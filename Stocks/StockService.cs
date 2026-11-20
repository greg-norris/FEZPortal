using System.Globalization;

namespace Stocks;

/// <summary>One price seen at <see cref="Time"/> (Unix seconds of the last trade).</summary>
readonly record struct Sample(long Time, double Price);

/// <summary>What the screens show for one symbol; an immutable copy, safe to read on the UI thread.</summary>
sealed record StockView(string Symbol, Profile? Profile, Quote? Quote, Sample[] Samples, string? Error, int Version) {
    public string Name => this.Profile?.Name ?? "";
}

/// <summary>
/// Polls Finnhub on a background thread and keeps the latest data for each
/// symbol. Calls are spaced at least <see cref="MinCallSpacingMs"/> apart, so
/// the app never goes over the free plan's 60 calls a minute.
///
/// The free plan has no price history, so the charts are built from the
/// quotes: every new last-trade time adds a sample. Samples are appended to
/// history.txt and reloaded at startup, so a restart keeps today's chart.
/// </summary>
sealed class StockService {
    private const int MinCallSpacingMs = 1100;          // at most ~54 calls a minute
    private const int OpenRefreshMs = 15_000;           // full watchlist refresh while the market is open
    private const int ClosedRefreshMs = 120_000;        // prices hardly move outside the session
    private const int MarketStatusRefreshMs = 120_000;
    private const int MetricsRefreshHours = 12;
    private const int MaxSamples = 1500;

    private readonly Settings settings;
    private readonly FinnhubClient? client;
    private readonly string historyPath, profilePath;
    private readonly object sync = new();
    private readonly Dictionary<string, Entry> entries = new();
    private long lastCallAt;
    private volatile string status = "Starting…";
    private volatile bool statusIsError;
    private long lastUpdateUnix;
    private MarketStatus? market;
    private int version;

    private sealed class Entry {
        public required string Symbol;
        public Profile? Profile;
        public DateTime ProfileFetched;
        public Quote? Quote;
        public List<Sample> Samples = new();
        public string? Error;
        public int Version;
        public StockView? View;     // cached copy for the UI, rebuilt when Version changes
    }

    public StockService(Settings settings, string dataFolder) {
        this.settings = settings;
        this.historyPath = Path.Combine(dataFolder, "history.txt");
        this.profilePath = Path.Combine(dataFolder, "profiles.txt");
        foreach (var s in settings.Symbols) this.entries[s] = new Entry { Symbol = s };
        if (settings.Problem is null) this.client = new FinnhubClient(settings.ApiKey);
        else this.SetStatus(settings.Problem, true);
    }

    public IReadOnlyList<string> Symbols => this.settings.Symbols;

    /// <summary>Changes whenever anything shown on screen changes.</summary>
    public int Version => Volatile.Read(ref this.version);

    public string Status => this.status;
    public bool StatusIsError => this.statusIsError;
    public long LastUpdateUnix => Interlocked.Read(ref this.lastUpdateUnix);

    public MarketStatus? Market {
        get { lock (this.sync) return this.market; }
    }

    public StockView Get(string symbol) {
        lock (this.sync) {
            var e = this.entries[symbol];
            if (e.View is null || e.View.Version != e.Version) {
                e.View = new StockView(e.Symbol, e.Profile, e.Quote, e.Samples.ToArray(), e.Error, e.Version);
            }
            return e.View;
        }
    }

    public void Start() {
        if (this.client is null) return;
        var thread = new Thread(() => this.RunAsync(this.client).GetAwaiter().GetResult()) { IsBackground = true, Name = "finnhub" };
        thread.Start();
    }

    // -- Polling --------------------------------------------------------------

    private async Task RunAsync(FinnhubClient api) {
        this.LoadProfiles();
        this.LoadHistory();
        var cancel = CancellationToken.None;
        long nextMarketCheck = 0;

        while (true) {
            var cycleStart = Environment.TickCount64;
            var failed = false;
            try {
                if (cycleStart >= nextMarketCheck) {
                    await this.ThrottleAsync().ConfigureAwait(false);
                    var status = await api.GetMarketStatusAsync(cancel).ConfigureAwait(false);
                    MarketClock.Calibrate(status.Time);
                    lock (this.sync) this.market = status;
                    this.Changed();
                    nextMarketCheck = cycleStart + MarketStatusRefreshMs;
                }

                // Quotes first, so the screen fills quickly ...
                var done = 0;
                foreach (var symbol in this.settings.Symbols) {
                    if (done == 0) this.SetStatus("Updating…", false);
                    await this.ThrottleAsync().ConfigureAwait(false);
                    await this.UpdateQuoteAsync(api, symbol, cancel).ConfigureAwait(false);
                    done++;
                }
                Interlocked.Exchange(ref this.lastUpdateUnix, MarketClock.UnixNow);
                this.SetStatus("", false);

                // ... then a few missing or stale company profiles per pass.
                var fetched = 0;
                foreach (var symbol in this.settings.Symbols) {
                    // Missing names load right away; stale metrics refresh a few at a time.
                    if (fetched >= 3 && this.HasProfile(symbol)) continue;
                    Entry e;
                    lock (this.sync) e = this.entries[symbol];
                    if (DateTime.UtcNow - e.ProfileFetched < TimeSpan.FromHours(MetricsRefreshHours)) continue;
                    if (e.Error is not null && e.Quote is null) continue;   // unknown symbol
                    fetched++;
                    await this.UpdateProfileAsync(api, e, cancel).ConfigureAwait(false);
                }
                if (fetched > 0) this.SaveProfiles();
            }
            catch (FinnhubException ex) when (ex.Status == System.Net.HttpStatusCode.TooManyRequests) {
                this.SetStatus("Finnhub rate limit reached; waiting a minute", true);
                await Task.Delay(60_000).ConfigureAwait(false);
                continue;
            }
            catch (Exception ex) {
                failed = true;
                this.SetStatus(FinnhubClient.Describe(ex), true);
                Console.Error.WriteLine($"Finnhub: {ex.GetType().Name}: {ex.Message}");
            }

            var open = this.Market?.IsOpen ?? true;
            var period = failed ? 10_000 : open ? OpenRefreshMs : ClosedRefreshMs;
            var wait = cycleStart + period - Environment.TickCount64;
            if (wait > 0) await Task.Delay((int)wait).ConfigureAwait(false);
        }
    }

    private async Task UpdateQuoteAsync(FinnhubClient api, string symbol, CancellationToken cancel) {
        Quote? quote;
        try {
            quote = await api.GetQuoteAsync(symbol, cancel).ConfigureAwait(false);
        }
        catch (FinnhubException ex) when (ex.Status == System.Net.HttpStatusCode.Forbidden) {
            lock (this.sync) {
                var e = this.entries[symbol];
                e.Error = "Not available on the free Finnhub plan";
                e.Version++;
            }
            this.Changed();
            return;
        }

        List<Sample>? added = null;
        lock (this.sync) {
            var e = this.entries[symbol];
            if (quote is null) {
                e.Error = "Unknown symbol";
                e.Version++;
            }
            else {
                e.Error = null;
                if (e.Quote != quote) {
                    e.Quote = quote;
                    e.Version++;
                }
                added = AddSample(e, quote);
            }
        }
        if (added is not null) foreach (var s in added) this.AppendHistory(symbol, s);
        this.Changed();
    }

    /// <summary>Records the quote on the chart. Returns the new samples to save.</summary>
    private static List<Sample>? AddSample(Entry e, Quote q) {
        if (q.Time <= 0 || q.Current <= 0) return null;
        var date = MarketClock.SessionDate(q.Time);

        // A new trading day starts a new chart.
        if (e.Samples.Count > 0 && MarketClock.SessionDate(e.Samples[^1].Time) != date) e.Samples.Clear();
        if (e.Samples.Count > 0 && e.Samples[^1].Time >= q.Time) return null;
        var added = new List<Sample>(2);

        // The opening price is a real point at 9:30, so the chart starts there
        // even when the program was started later in the day.
        var (open, _) = MarketClock.Session(date);
        if (e.Samples.Count == 0 && q.Open > 0 && q.Time > open + 120) {
            var first = new Sample(open, q.Open);
            e.Samples.Add(first);
            added.Add(first);
        }

        var sample = new Sample(q.Time, q.Current);
        e.Samples.Add(sample);
        if (e.Samples.Count > MaxSamples) {
            // Thin the older half instead of dropping the start of the day.
            var thinned = new List<Sample>(MaxSamples);
            for (var i = 0; i < e.Samples.Count; i++) {
                if (i < e.Samples.Count / 2 && i % 2 == 1 && i != 0) continue;
                thinned.Add(e.Samples[i]);
            }
            e.Samples = thinned;
        }
        e.Version++;
        added.Add(sample);
        return added;
    }

    private async Task UpdateProfileAsync(FinnhubClient api, Entry e, CancellationToken cancel) {
        try {
            await this.ThrottleAsync().ConfigureAwait(false);
            var profile = await api.GetProfileAsync(e.Symbol, cancel).ConfigureAwait(false) ?? e.Profile ?? new Profile(e.Symbol, "", "", "USD", 0);
            try {
                await this.ThrottleAsync().ConfigureAwait(false);
                profile = await api.AddMetricsAsync(e.Symbol, profile, cancel).ConfigureAwait(false);
            }
            catch (FinnhubException ex) when (ex.Status == System.Net.HttpStatusCode.Forbidden) { }
            lock (this.sync) {
                e.Profile = profile;
                e.ProfileFetched = DateTime.UtcNow;
                e.Version++;
            }
            this.Changed();
        }
        catch (FinnhubException ex) when (ex.Status == System.Net.HttpStatusCode.Forbidden) {
            lock (this.sync) e.ProfileFetched = DateTime.UtcNow;
        }
    }

    private bool HasProfile(string symbol) {
        lock (this.sync) return this.entries[symbol].Profile is not null;
    }

    private async Task ThrottleAsync() {
        var wait = this.lastCallAt + MinCallSpacingMs - Environment.TickCount64;
        if (wait > 0) await Task.Delay((int)wait).ConfigureAwait(false);
        this.lastCallAt = Environment.TickCount64;
    }

    private void SetStatus(string text, bool error) {
        this.status = text;
        this.statusIsError = error;
        this.Changed();
    }

    private void Changed() => Interlocked.Increment(ref this.version);

    // -- Files ----------------------------------------------------------------
    // history.txt:  SYMBOL UNIXTIME PRICE        (one sample per line)
    // profiles.txt: SYMBOL|name|exchange|industry|currency|shares|52wHigh|52wLow|pe|yield|beta

    private void LoadHistory() {
        if (!File.Exists(this.historyPath)) return;
        try {
            var perSymbol = new Dictionary<string, List<Sample>>();
            foreach (var line in File.ReadLines(this.historyPath)) {
                var f = line.Split(' ');
                if (f.Length != 3 || !this.entries.ContainsKey(f[0])) continue;
                if (!long.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)) continue;
                if (!double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var p)) continue;
                if (!perSymbol.TryGetValue(f[0], out var list)) perSymbol[f[0]] = list = new List<Sample>();
                list.Add(new Sample(t, p));
            }

            // Keep each symbol's most recent trading day only, and rewrite the file with just that.
            using var writer = new StreamWriter(this.historyPath + ".new");
            lock (this.sync) {
                foreach (var (symbol, list) in perSymbol) {
                    list.Sort((a, b) => a.Time.CompareTo(b.Time));
                    var last = MarketClock.SessionDate(list[^1].Time);
                    var day = list.Where(s => MarketClock.SessionDate(s.Time) == last).ToList();
                    var e = this.entries[symbol];
                    e.Samples = day.Count > MaxSamples ? day.Skip(day.Count - MaxSamples).ToList() : day;
                    e.Version++;
                    foreach (var s in e.Samples) writer.WriteLine(Format(symbol, s));
                }
            }
            writer.Close();
            File.Move(this.historyPath + ".new", this.historyPath, true);
            this.Changed();
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"history.txt: {ex.Message}");
        }
    }

    private void AppendHistory(string symbol, Sample s) {
        try { File.AppendAllText(this.historyPath, Format(symbol, s) + "\n"); }
        catch (Exception ex) { Console.Error.WriteLine($"history.txt: {ex.Message}"); }
    }

    private static string Format(string symbol, Sample s) =>
        string.Create(CultureInfo.InvariantCulture, $"{symbol} {s.Time} {s.Price:0.####}");

    private void LoadProfiles() {
        if (!File.Exists(this.profilePath)) return;
        try {
            foreach (var line in File.ReadLines(this.profilePath)) {
                var f = line.Split('|');
                if (f.Length < 11 || !this.entries.TryGetValue(f[0], out var e)) continue;
                var profile = new Profile(f[1], f[2], f[3], f[4], N(f[5]) ?? 0) {
                    WeekHigh52 = N(f[6]), WeekLow52 = N(f[7]), PeRatio = N(f[8]), DividendYield = N(f[9]), Beta = N(f[10]),
                };
                lock (this.sync) {
                    e.Profile = profile;
                    e.Version++;
                }
            }
            this.Changed();
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"profiles.txt: {ex.Message}");
        }

        static double? N(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private void SaveProfiles() {
        try {
            var lines = new List<string>();
            lock (this.sync) {
                foreach (var e in this.entries.Values) {
                    if (e.Profile is not { } p) continue;
                    lines.Add(string.Join('|', e.Symbol, Clean(p.Name), Clean(p.Exchange), Clean(p.Industry), Clean(p.Currency),
                                          N(p.SharesMillions), N(p.WeekHigh52), N(p.WeekLow52), N(p.PeRatio), N(p.DividendYield), N(p.Beta)));
                }
            }
            File.WriteAllLines(this.profilePath, lines);
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"profiles.txt: {ex.Message}");
        }

        static string Clean(string s) => s.Replace('|', '/').Replace('\n', ' ');
        static string N(double? v) => v?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    }
}
