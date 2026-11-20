using System;
using System.Diagnostics;
using System.Threading;
using GHIElectronics.TinyCLR.Native;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.Device {
    /// <summary>
    /// The background thread that talks to the internet: joins Wi-Fi, sets the clock, finds the
    /// location, and keeps the weather and stock quotes fresh. It makes one request per step, so
    /// a slow stock round never holds up the weather, and it writes results into PortalState.
    /// </summary>
    public class DataService : IPortalHost {
        const int TimeSyncHours = 6;
        const int LocationHours = 12;
        const int WeatherMinutes = 15;
        const int MarketMinutes = 5;
        const int StocksOpenSeconds = 60;      // a full watchlist round while the market is open
        const int StocksClosedSeconds = 600;   // prices hardly move outside the session
        const int RetrySeconds = 60;
        const int FinnhubSpacingMs = 1100;     // the free plan allows 60 calls a minute

        readonly PortalState state;
        readonly Storage storage;
        readonly Wifi wifi;
        readonly Buzzer buzzer = new Buzzer();
        const int MetricsHours = 12;

        SettingsFile profiles;                  // cache\profiles.txt: SYMBOL=name|exchange|industry|shares
        volatile bool weatherRequested, stocksRequested, scanRequested;
        volatile WifiNetwork joinRequested;     // a network picked on the Wi-Fi screen
        DateTime nextTime, nextLocation, nextWeather, nextMarket, nextStocks, nextSignal, lastFinnhub, nextCalendar;
        int stockIndex;
        int autoJoinIndex;
        int failures;                           // quote requests failed in a row
        DateTime nextMemoryLog;                      // round robin over saved networks when scanning fails

        public DataService(PortalState state, Storage storage) {
            this.state = state;
            this.storage = storage;
            this.wifi = new Wifi(state);
        }

        public void Start() {
            new Thread(this.Run).Start();
        }

        // -- IPortalHost (called from the UI thread) ----------------------------

        public bool SaveSettings(string fileName, string text) => this.storage.WriteText(fileName, text);

        public void RefreshWeather() => this.weatherRequested = true;

        public void RefreshStocks() => this.stocksRequested = true;

        public void RefreshCalendar() => this.nextCalendar = DateTime.MinValue;

        public void ScanWifi() {
            lock (this.state) {
                this.state.WifiScanning = true;
                this.state.WifiScanMessage = "";
                this.state.WifiVersion++;
            }
            this.scanRequested = true;
        }

        public void JoinWifi(string ssid, string password) => this.joinRequested = new WifiNetwork(ssid, password);

        public void Ring(bool on) => this.buzzer.Ring(on);

        public void Chirp() => this.buzzer.Chirp();

        // -- Thread --------------------------------------------------------------

        void Run() {
            this.LoadCaches();
            LogMemory("at startup");
            while (true) {
                try {
                    // Requests from the Wi-Fi screen come first.
                    var join = this.joinRequested;
                    if (join != null) {
                        this.joinRequested = null;
                        if (this.Join(join, true)) continue;
                        this.Wait(5000);   // leave "Can't join ..." up for a moment before falling back
                        continue;
                    }
                    if (this.scanRequested) {
                        this.scanRequested = false;
                        this.ScanNow();
                        continue;
                    }

                    if (!this.wifi.Connected) {
                        if (!this.AutoJoin()) {
                            this.SetMessages("No Wi-Fi");
                            this.Wait(15000);
                        }
                        continue;
                    }
                    this.Step();
                }
                catch (Exception ex) {
                    Debug.WriteLine("Data: " + ex.GetType().Name + ": " + ex.Message);
                }
                Thread.Sleep(200);
            }
        }

        /// <summary>Sleeps, but wakes early for a request from the Wi-Fi screen.</summary>
        void Wait(int ms) {
            for (var waited = 0; waited < ms && this.joinRequested == null && !this.scanRequested; waited += 100) Thread.Sleep(100);
        }

        // -- Wi-Fi -----------------------------------------------------------------

        /// <summary>
        /// Picks a saved network and joins it: the first one, or with auto-join on, the first saved
        /// network a scan finds in range (taking turns through them if scanning doesn't work).
        /// </summary>
        bool AutoJoin() {
            System.Collections.ArrayList saved;
            bool autoJoin;
            lock (this.state) {
                saved = this.state.Config.WifiNetworks;
                autoJoin = this.state.Config.WifiAutoJoin;
            }
            if (saved.Count == 0) {
                this.SetWifiStatus(WifiStatus.Failed, "No saved Wi-Fi network");
                return false;
            }

            var pick = (WifiNetwork)saved[0];
            if (autoJoin && saved.Count > 1) {
                var seen = this.ScanNow();
                if (seen != null) {
                    pick = null;
                    foreach (WifiNetwork n in saved) {
                        if (Contains(seen, n.Ssid)) { pick = n; break; }
                    }
                    if (pick == null) {
                        this.SetWifiStatus(WifiStatus.Failed, "No saved network in range");
                        return false;
                    }
                }
                else {
                    pick = (WifiNetwork)saved[this.autoJoinIndex++ % saved.Count];
                }
            }
            return this.Join(pick, false);
        }

        /// <summary>Joins a network and moves it to the top of the saved list.</summary>
        bool Join(WifiNetwork network, bool requested) {
            if (!this.wifi.Connect(network.Ssid, network.Password)) return false;

            string text = null;
            lock (this.state) {
                if (this.state.Config.RememberWifi(network.Ssid, network.Password)) text = this.state.Config.WifiText();
                this.state.WifiVersion++;
            }
            if (text != null && !this.storage.WriteText(DefaultFiles.WifiName, text) && requested) {
                Debug.WriteLine("Wi-Fi: joined " + network.Ssid + " but couldn't save it (no SD card?)");
            }
            this.SetMessages("Loading...");
            this.ResetSchedule();
            return true;
        }

        /// <summary>Scans and publishes the result; returns the names seen, or null if the scan failed.</summary>
        string[] ScanNow() {
            lock (this.state) {
                this.state.WifiScanning = true;
                this.state.WifiVersion++;
            }
            string[] seen = null;
            string message = "";
            try {
                seen = this.wifi.Scan();
                if (seen.Length == 0) message = "No networks found";
            }
            catch (Exception ex) {
                Debug.WriteLine("Wi-Fi scan: " + ex.Message);
                message = "Scan failed: " + ex.Message;
            }
            lock (this.state) {
                if (seen != null) this.state.WifiScan = seen;
                this.state.WifiScanning = false;
                this.state.WifiScanMessage = message;
                this.state.WifiVersion++;
            }
            return seen;
        }

        static bool Contains(string[] names, string name) {
            foreach (var n in names) {
                if (n == name) return true;
            }
            return false;
        }

        void SetWifiStatus(WifiStatus status, string message) {
            lock (this.state) {
                this.state.Wifi = status;
                this.state.WifiMessage = message;
                this.state.StatusVersion++;
            }
        }

        /// <summary>Does the most urgent piece of work, one request at most.</summary>
        void Step() {
            var now = Clock.UtcNow;
            var config = this.state.Config;

            if (now >= this.nextTime) { this.SyncTime(config.NtpServer); return; }
            if ((config.LocationAuto || config.OffsetAuto) && now >= this.nextLocation) { this.UpdateLocation(); return; }
            if (this.weatherRequested || now >= this.nextWeather) { this.UpdateWeather(); return; }
            if (config.CalendarUrls.Count > 0 && now >= this.nextCalendar) { this.UpdateCalendar(); return; }

            if (config.FinnhubKey.Length > 0 && this.state.Stocks.Length > 0 && now >= this.lastFinnhub.AddMilliseconds(FinnhubSpacingMs)) {
                if (now >= this.nextMarket) { this.UpdateMarket(); return; }
                if (this.stocksRequested || now >= this.nextStocks) { this.UpdateNextStock(); return; }
                if (this.UpdateNextDetails()) return;
            }

            if (now >= this.nextSignal) {
                this.wifi.UpdateSignal();
                this.nextSignal = now.AddSeconds(30);
            }
        }

        void ResetSchedule() {
            var now = Clock.UtcNow;
            this.nextTime = this.nextLocation = this.nextWeather = this.nextMarket = this.nextStocks = this.nextSignal = this.nextCalendar = now;
            this.lastFinnhub = DateTime.MinValue;
            this.stockIndex = 0;
        }

        // -- Time ----------------------------------------------------------------

        void SyncTime(string server) {
            var before = Clock.UtcNow;
            try {
                var utc = TimeSync.SyncFromNetwork(server);
                lock (this.state) {
                    this.state.TimeSynced = true;
                    this.state.LastTimeSync = utc;
                    this.state.TimeVersion++;
                }
                // The schedule is kept in clock time; after a big jump start it over.
                var jump = (utc - before).Ticks / TimeSpan.TicksPerSecond;
                if (jump > 60 || jump < -60) this.ResetSchedule();
                this.nextTime = utc.AddHours(TimeSyncHours);
            }
            catch (Exception ex) {
                Debug.WriteLine("Time: " + ex.Message);
                this.nextTime = Clock.UtcNow.AddSeconds(RetrySeconds);
            }
        }

        // -- Location and weather ---------------------------------------------

        void UpdateLocation() {
            var config = this.state.Config;
            try {
                var location = Location.ParseLookup(Http.Get(Location.LookupUrl));
                this.storage.WriteText("cache\\location.txt", location.ToText());
                lock (this.state) {
                    if (config.LocationAuto) this.state.Location = location;
                    if (config.OffsetAuto && location.HasOffset) this.SetOffset(location.UtcOffsetSeconds, location.TimeZone);
                    this.state.WeatherVersion++;
                }
                this.nextLocation = Clock.UtcNow.AddHours(LocationHours);
            }
            catch (Exception ex) {
                Debug.WriteLine("Location: " + ex.Message);
                this.nextLocation = Clock.UtcNow.AddSeconds(RetrySeconds * 5);
                if (this.state.Location == null) {
                    lock (this.state) {
                        this.state.WeatherMessage = "Can't find your location. Set it in weather.txt.";
                        this.state.WeatherVersion++;
                    }
                }
            }
        }

        void UpdateWeather() {
            this.weatherRequested = false;
            var location = this.state.Location;
            if (location == null) {
                // Waiting for the location lookup.
                this.nextWeather = Clock.UtcNow.AddSeconds(10);
                return;
            }
            try {
                var weather = WeatherData.Parse(Http.Get(WeatherData.Url(location.Latitude, location.Longitude, this.state.Config.Metric)));
                lock (this.state) {
                    this.state.Weather = weather;
                    this.state.WeatherMessage = "";
                    if (this.state.Config.OffsetAuto) this.SetOffset(weather.UtcOffsetSeconds, weather.TimeZone);
                    this.state.WeatherVersion++;
                }
                this.nextWeather = Clock.UtcNow.AddMinutes(WeatherMinutes);
            }
            catch (Exception ex) {
                Debug.WriteLine("Weather: " + ex.Message);
                lock (this.state) {
                    this.state.WeatherMessage = "Weather failed: " + ex.Message;
                    this.state.WeatherVersion++;
                }
                this.nextWeather = Clock.UtcNow.AddSeconds(RetrySeconds * 2);
            }
        }

        /// <summary>Call inside lock(state).</summary>
        void SetOffset(int seconds, string zone) {
            if (Clock.OffsetSeconds == seconds && this.state.TimeZone == zone) return;
            Clock.OffsetSeconds = seconds;
            this.state.TimeZone = zone ?? "";
            this.state.TimeVersion++;
            this.state.CalendarVersion++;   // "today" may have moved
            this.nextCalendar = Clock.UtcNow;   // online events are converted to local time: redo them
        }

        // -- Online calendar -----------------------------------------------------

        const int CalendarMinutes = 15;
        const int CalendarDaysBack = 45, CalendarDaysAhead = 400;

        /// <summary>
        /// Downloads the ics= calendars from calendar.txt (streamed: Outlook's feed is ~200 KB),
        /// expands repeats into local-time events for the months around today, and caches them.
        /// </summary>
        void UpdateCalendar() {
            var urls = this.state.Config.CalendarUrls;
            var now = Clock.Now;
            var from = new DateTime(now.Year, now.Month, 1).AddDays(-CalendarDaysBack);
            var to = now.Date.AddDays(CalendarDaysAhead);
            var all = new System.Collections.ArrayList();
            lock (this.state) {
                this.state.CalendarSyncing = true;
                this.state.CalendarVersion++;
            }
            try {
                foreach (string url in urls) {
                    var started = DateTime.UtcNow;
                    Debug.WriteLine("Calendar: downloading " + url);
                    var feed = new Ics();
                    Http.GetLines(url, feed);
                    feed.Finish();
                    Debug.WriteLine("Calendar: " + feed.EventCount + " events read in " + (DateTime.UtcNow - started).Ticks / TimeSpan.TicksPerSecond + " s");
                    foreach (var e in feed.Expand(from, to, Clock.UtcNow, Clock.OffsetSeconds)) all.Add(e);
                }
                lock (this.state) {
                    this.state.OnlineEvents = all;
                    this.state.CalendarSyncing = false;
                    this.state.CalendarMessage = "";
                    this.state.CalendarUpdated = Clock.UtcNow;
                    this.state.CalendarVersion++;
                }
                Debug.WriteLine("Calendar: " + all.Count + " events from " + Format.ShortDate(from) + " to " + Format.ShortDate(to));
                this.storage.WriteText("cache\\calendar.txt", CalendarEvents.ToText(all));
                this.nextCalendar = Clock.UtcNow.AddMinutes(CalendarMinutes);
            }
            catch (Exception ex) {
                var http = ex as HttpException;
                Debug.WriteLine("Calendar: " + ex.GetType().Name + " " + ex.Message);
                lock (this.state) {
                    // Say what went wrong, so it can be seen without a debugger.
                    this.state.CalendarMessage = http != null ? "Calendar link answered " + http.Status
                        : "Calendar failed: " + ex.GetType().Name + (ex.Message != null && ex.Message.Length > 0 ? " " + ex.Message : "");
                    this.state.CalendarSyncing = false;
                    this.state.CalendarVersion++;
                }
                if (http == null) FreeMemory("calendar failed");
                this.nextCalendar = Clock.UtcNow.AddMinutes(5);
            }
        }

        // -- Stocks --------------------------------------------------------------

        void UpdateMarket() {
            var config = this.state.Config;
            try {
                this.lastFinnhub = Clock.UtcNow;
                string session, holiday;
                var open = Finnhub.ParseMarket(Http.Get(Finnhub.MarketUrl(config.FinnhubKey)), out session, out holiday);
                lock (this.state) {
                    var changed = !this.state.HasMarketStatus || open != this.state.MarketOpen || session != this.state.MarketSession;
                    this.state.HasMarketStatus = true;
                    this.state.MarketOpen = open;
                    this.state.MarketSession = session;
                    this.state.MarketHoliday = holiday;
                    if (changed) this.state.MarketVersion++;
                }
                this.nextMarket = Clock.UtcNow.AddMinutes(MarketMinutes);
            }
            catch (Exception ex) {
                this.StocksFailed(ex);
                this.nextMarket = Clock.UtcNow.AddSeconds(RetrySeconds);
            }
        }

        /// <summary>One quote per call, going round the watchlist. Company details wait until a round is done.</summary>
        void UpdateNextStock() {
            var stocks = this.state.Stocks;
            if (this.stockIndex >= stocks.Length) this.stockIndex = 0;
            var item = stocks[this.stockIndex];
            var key = this.state.Config.FinnhubKey;

            try {
                this.lastFinnhub = Clock.UtcNow;
                var quote = Quote.Parse(Http.Get(Finnhub.QuoteUrl(item.Symbol, key)));
                lock (this.state) {
                    var changed = this.state.StocksMessage.Length > 0;
                    this.state.StocksMessage = "";
                    this.state.StocksUpdated = Clock.UtcNow;
                    if (quote == null) {
                        changed |= item.Error == null;
                        item.Error = "Unknown symbol";
                    }
                    else {
                        changed |= item.Update(quote);
                    }
                    // Redraw only for news: outside market hours most quotes are unchanged.
                    if (changed) this.state.StocksVersion++;
                }
                this.failures = 0;
            }
            catch (Exception ex) {
                var http = ex as HttpException;
                if (http != null && http.Status == 403) {
                    // This symbol isn't on the free plan; carry on with the others.
                    lock (this.state) {
                        item.Error = "Needs a paid Finnhub plan";
                        this.state.StocksVersion++;
                    }
                }
                else {
                    this.StocksFailed(ex);
                    this.nextStocks = Clock.UtcNow.AddSeconds(this.RetryDelay(http));
                    return;
                }
            }

            this.stockIndex++;
            if (this.stockIndex >= stocks.Length) {
                // Round finished: wait for the next one.
                this.stockIndex = 0;
                this.stocksRequested = false;
                this.nextStocks = Clock.UtcNow.AddSeconds(this.state.MarketOpen ? StocksOpenSeconds : StocksClosedSeconds);
                if (Clock.UtcNow >= this.nextMemoryLog) {
                    LogMemory("after a stock round");
                    this.nextMemoryLog = Clock.UtcNow.AddMinutes(5);
                }
            }
        }

        /// <summary>
        /// Between quote rounds: the company profile (once, then cached on the SD card) or the
        /// 52-week range, P/E and yield (twice a day) of one symbol. False when there is nothing to do.
        /// </summary>
        bool UpdateNextDetails() {
            var now = Clock.UtcNow;
            foreach (var item in this.state.Stocks) {
                if (!item.HasQuote || now < item.DetailsRetry) continue;
                if (!item.HasProfile) { this.UpdateProfile(item); return true; }
                if (now >= item.MetricsDue) { this.UpdateMetrics(item); return true; }
            }
            return false;
        }

        void UpdateProfile(StockItem item) {
            this.lastFinnhub = Clock.UtcNow;
            try {
                var profile = Profile.Parse(Http.Get(Finnhub.ProfileUrl(item.Symbol, this.state.Config.FinnhubKey)));
                lock (this.state) {
                    item.SetProfile(profile);
                    this.state.StocksVersion++;
                }
                profile.Name = item.Name;
                this.profiles.Set(item.Symbol, profile.ToText());
                this.storage.WriteText("cache\\profiles.txt", this.profiles.ToText());
            }
            catch (Exception ex) {
                this.DetailsFailed(item, "Profile", ex);
            }
        }

        void UpdateMetrics(StockItem item) {
            this.lastFinnhub = Clock.UtcNow;
            try {
                var url = Finnhub.MetricUrl(item.Symbol, this.state.Config.FinnhubKey);
                var metrics = Metrics.Parse(Http.Get(url, Metrics.MaxBytes, Metrics.EndMarker));
                lock (this.state) {
                    item.SetMetrics(metrics);
                    this.state.StocksVersion++;
                }
                item.MetricsDue = Clock.UtcNow.AddHours(MetricsHours);
            }
            catch (Exception ex) {
                var http = ex as HttpException;
                if (http != null && http.Status == 403) item.MetricsDue = Clock.UtcNow.AddHours(24);   // not on this plan
                else this.DetailsFailed(item, "Metrics", ex);
            }
        }

        /// <summary>Details are extras: log, free memory, and try this symbol again in a few minutes.</summary>
        void DetailsFailed(StockItem item, string what, Exception ex) {
            Debug.WriteLine(what + " " + item.Symbol + ": " + ex.GetType().Name + " " + ex.Message);
            item.DetailsRetry = Clock.UtcNow.AddMinutes(5);
            if (!(ex is HttpException)) FreeMemory(what + " failed");
        }

        /// <summary>Seconds before trying a failed quote again: quickly at first, then backing off.</summary>
        int RetryDelay(HttpException http) {
            if (http != null && http.Status == 429) return 60;
            this.failures++;
            return this.failures <= 3 ? 3 : 15;
        }

        void StocksFailed(Exception ex) {
            Debug.WriteLine("Stocks: " + ex.GetType().Name + " " + ex.Message);
            var http = ex as HttpException;
            if (http == null) {
                // Usually a connection that couldn't be set up (often TLS running short of memory).
                // Keep showing what we have, free what we can and try again shortly.
                FreeMemory("quote failed");
                lock (this.state) {
                    this.state.StocksMessage = "Connection to Finnhub failed, retrying";
                    if (!this.state.HasMarketStatus) this.state.StocksVersion++;
                }
                return;
            }
            string message;
            if (http.Status == 401) message = "Finnhub rejected the API key";
            else if (http.Status == 429) message = "Finnhub rate limit, waiting";
            else message = "Finnhub answered " + http.Status;
            lock (this.state) {
                this.state.StocksMessage = message;
                this.state.HasMarketStatus = false;
                this.state.MarketVersion++;
                this.state.StocksVersion++;
            }
        }

        // -- Memory ----------------------------------------------------------------

        /// <summary>Collects garbage (so TLS can find its 16 KB block) and logs what is left.</summary>
        static void FreeMemory(string why) {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            LogMemory(why);
        }

        static void LogMemory(string when) {
            try {
                Debug.WriteLine("Memory " + when + ": managed " + Memory.ManagedMemory.FreeBytes / 1024 + " KB free, " +
                                Memory.ManagedMemory.UsedBytes / 1024 + " KB used; unmanaged " +
                                Memory.UnmanagedMemory.FreeBytes / 1024 + " KB free; extended heap " + Memory.IsExtendedHeap());
            }
            catch (Exception ex) {
                Debug.WriteLine("Memory: " + ex.Message);
            }
        }

        // -- Startup ---------------------------------------------------------------

        void LoadCaches() {
            var config = this.state.Config;
            this.profiles = new SettingsFile(this.storage.ReadText("cache\\profiles.txt"));
            var names = new SettingsFile(this.storage.ReadText("cache\\names.txt"));   // older versions kept only names

            lock (this.state) {
                foreach (var item in this.state.Stocks) {
                    var profile = Profile.FromText(this.profiles.Get(item.Symbol, null));
                    if (profile != null) item.SetProfile(profile);
                    else item.Name = names.Get(item.Symbol, "");
                }
                this.state.StocksVersion++;

                if (!config.LocationAuto) {
                    this.state.Location = new Location { Place = config.PlaceName, Latitude = config.Latitude, Longitude = config.Longitude };
                }
                // The last online calendar download, until a fresh one arrives.
                if (config.CalendarUrls.Count > 0) {
                    this.state.OnlineEvents = CalendarEvents.Parse(this.storage.ReadText("cache\\calendar.txt"));
                    this.state.CalendarVersion++;
                }

                var cached = Location.FromText(this.storage.ReadText("cache\\location.txt"));
                if (cached != null) {
                    if (config.LocationAuto) this.state.Location = cached;
                    if (config.OffsetAuto && cached.HasOffset) this.SetOffset(cached.UtcOffsetSeconds, cached.TimeZone);
                }
                if (!config.OffsetAuto) {
                    Clock.OffsetSeconds = config.FixedOffsetSeconds;
                    this.state.TimeZone = "";
                    this.state.TimeVersion++;
                }
                this.state.WeatherVersion++;
            }
        }

        void SetMessages(string message) {
            lock (this.state) {
                if (this.state.Weather == null) {
                    this.state.WeatherMessage = message;
                    this.state.WeatherVersion++;
                }
                this.state.StocksMessage = message;
                this.state.StocksVersion++;
            }
        }
    }
}
