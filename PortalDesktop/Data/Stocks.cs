using System;

namespace PortalDesktop.Data {
    /// <summary>
    /// One symbol on the watchlist. The free Finnhub plan has no price history, so the chart is
    /// built from the quotes seen while the Portal runs (like the Endpoint Stocks program).
    /// Samples live in arrays with spare room (SampleCount of them are used), so a new quote
    /// doesn't allocate anything; both threads only touch them inside lock(state).
    /// </summary>
    public class StockItem {
        public const int MaxSamples = 390;   // one per minute over a 6.5 hour session

        public string Symbol;
        public string Name = "";
        public bool HasQuote;
        public double Current, Change, ChangePercent, High, Low, Open, PrevClose;
        public long Time;                   // Unix seconds of the last trade
        public string Error;

        public long[] SampleTimes = new long[0];
        public double[] SamplePrices = new double[0];
        public int SampleCount;

        // From /stock/profile2 (cached on the SD card).
        public bool HasProfile;
        public string Exchange = "";
        public string Industry = "";
        public double Shares;               // shares outstanding, millions

        // From /stock/metric (refreshed twice a day); 0 = not known.
        public double High52, Low52, PE, DividendYield;
        public DateTime MetricsDue = DateTime.MinValue;   // UTC
        public DateTime DetailsRetry = DateTime.MinValue; // UTC; after a failed profile/metrics request

        /// <summary>Market capitalisation in millions of dollars at the current price (0 = unknown).</summary>
        public double MarketCap => this.Shares > 0 && this.HasQuote ? this.Shares * this.Current : 0;

        public StockItem(string symbol) {
            this.Symbol = symbol;
        }

        public bool IsUp => this.Change >= 0;

        /// <summary>Copies a new quote in. Returns false when nothing changed.</summary>
        public bool Update(Quote q) {
            if (this.HasQuote && q.Time == this.Time && q.Current == this.Current) return false;
            var newDay = this.Time > 0 && q.Time / 86400 != this.Time / 86400;
            this.HasQuote = true;
            this.Error = null;
            this.Current = q.Current;
            this.Change = q.Change;
            this.ChangePercent = q.ChangePercent;
            this.High = q.High;
            this.Low = q.Low;
            this.Open = q.Open;
            this.PrevClose = q.PrevClose;
            if (newDay) this.SampleCount = 0;   // keep the arrays for the new day
            if (q.Time > this.Time) this.AddSample(q.Time, q.Current);
            this.Time = q.Time;
            return true;
        }

        public void SetProfile(Profile p) {
            this.HasProfile = true;
            if (p.Name.Length > 0) this.Name = p.Name;
            else if (this.Name.Length == 0) this.Name = this.Symbol;   // funds have no profile on the free plan
            this.Exchange = p.Exchange;
            this.Industry = p.Industry;
            this.Shares = p.Shares;
        }

        public void SetMetrics(Metrics m) {
            this.High52 = m.High52;
            this.Low52 = m.Low52;
            this.PE = m.PE;
            this.DividendYield = m.DividendYield;
        }

        void AddSample(long time, double price) {
            var n = this.SampleCount;
            if (n == MaxSamples) {
                // Full: drop the oldest.
                Array.Copy(this.SampleTimes, 1, this.SampleTimes, 0, n - 1);
                Array.Copy(this.SamplePrices, 1, this.SamplePrices, 0, n - 1);
                n--;
            }
            else if (n == this.SampleTimes.Length) {
                // Grow in a few big steps (64, 128, 256, 390) rather than one sample at a time.
                var size = n == 0 ? 64 : n * 2;
                if (size > MaxSamples) size = MaxSamples;
                var times = new long[size];
                var prices = new double[size];
                Array.Copy(this.SampleTimes, times, n);
                Array.Copy(this.SamplePrices, prices, n);
                this.SampleTimes = times;
                this.SamplePrices = prices;
            }
            this.SampleTimes[n] = time;
            this.SamplePrices[n] = price;
            this.SampleCount = n + 1;
        }
    }

    /// <summary>A Finnhub /quote answer.</summary>
    public class Quote {
        public double Current, Change, ChangePercent, High, Low, Open, PrevClose;
        public long Time;

        /// <summary>The quote, or null for a symbol Finnhub doesn't know (it answers with zeros).</summary>
        public static Quote Parse(string json) {
            var r = Json.Parse(json);
            var q = new Quote {
                Current = Json.Num(r, "c", 0),
                High = Json.Num(r, "h", 0),
                Low = Json.Num(r, "l", 0),
                Open = Json.Num(r, "o", 0),
                PrevClose = Json.Num(r, "pc", 0),
                Time = (long)Json.Num(r, "t", 0),
            };
            if (q.Current <= 0 && q.Time == 0) return null;
            q.Change = Json.Num(r, "d", q.Current - q.PrevClose);
            q.ChangePercent = Json.Num(r, "dp", q.PrevClose > 0 ? q.Change / q.PrevClose * 100 : 0);
            return q;
        }
    }

    /// <summary>Company details from /stock/profile2, kept in cache\profiles.txt as name|exchange|industry|shares.</summary>
    public class Profile {
        public string Name = "", Exchange = "", Industry = "";
        public double Shares;

        public static Profile Parse(string json) {
            var r = Json.Parse(json);
            return new Profile {
                Name = Clean(Json.Str(r, "name")),
                Exchange = Finnhub.ShortExchange(Clean(Json.Str(r, "exchange"))),
                Industry = Clean(Json.Str(r, "finnhubIndustry")),
                Shares = Json.Num(r, "shareOutstanding", 0),
            };
        }

        public string ToText() => this.Name + "|" + this.Exchange + "|" + this.Industry + "|" + Ui.Format.Plain(this.Shares, 4);

        public static Profile FromText(string text) {
            if (text == null || text.Length == 0) return null;
            var parts = text.Split('|');
            var p = new Profile { Name = parts[0] };
            if (parts.Length >= 4) {
                p.Exchange = parts[1];
                p.Industry = parts[2];
                try {
                    var v = Json.Parse(parts[3]);
                    if (v is double) p.Shares = (double)v;
                }
                catch { }
            }
            return p;
        }

        /// <summary>Drops the characters the cache file uses as separators.</summary>
        static string Clean(string s) {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder();
            // Indexed loop: foreach over a string doesn't compile against the TinyCLR mscorlib (CS7038).
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c != '|' && c != '\n' && c != '\r') sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// The few numbers the detail view shows from /stock/metric. That answer can be hundreds of KB
    /// (it carries years of history after the "metric" object), so only its start is downloaded and
    /// the keys are picked out of the text instead of parsing it all.
    /// </summary>
    public class Metrics {
        public const int MaxBytes = 24576;

        /// <summary>The key that follows the "metric" object: the download stops once it arrives.</summary>
        public const string EndMarker = "\"metricType\"";

        public double High52, Low52, PE, DividendYield;

        public static Metrics Parse(string text) => new Metrics {
            High52 = Find(text, "52WeekHigh"),
            Low52 = Find(text, "52WeekLow"),
            PE = First(Find(text, "peTTM"), Find(text, "peBasicExclExtraTTM"), Find(text, "peExclExtraTTM")),
            DividendYield = First(Find(text, "dividendYieldIndicatedAnnual"), Find(text, "currentDividendYieldTTM"), 0),
        };

        static double First(double a, double b, double c) => a != 0 ? a : b != 0 ? b : c;

        /// <summary>The number after "key":, or 0 when it is missing, null or cut off.</summary>
        static double Find(string text, string key) {
            var at = text.IndexOf("\"" + key + "\":");
            if (at < 0) return 0;
            var i = at + key.Length + 3;
            while (i < text.Length && text[i] == ' ') i++;
            var end = i;
            while (end < text.Length && (text[end] == '-' || text[end] == '.' || text[end] == 'e' || text[end] == 'E' || text[end] == '+' ||
                                         (text[end] >= '0' && text[end] <= '9'))) end++;
            if (end == i || end == text.Length) return 0;
            try {
                var v = Json.Parse(text.Substring(i, end - i));
                return v is double ? (double)v : 0;
            }
            catch {
                return 0;
            }
        }
    }

    /// <summary>The few Finnhub calls the Portal makes (https://finnhub.io/docs/api).</summary>
    public static class Finnhub {
        public const string Base = "https://finnhub.io/api/v1/";

        public static string QuoteUrl(string symbol, string key) => Base + "quote?symbol=" + symbol + "&token=" + key;
        public static string ProfileUrl(string symbol, string key) => Base + "stock/profile2?symbol=" + symbol + "&token=" + key;
        public static string MarketUrl(string key) => Base + "stock/market-status?exchange=US&token=" + key;
        public static string MetricUrl(string symbol, string key) => Base + "stock/metric?symbol=" + symbol + "&metric=all&token=" + key;

        /// <summary>"NASDAQ NMS - GLOBAL MARKET" -> "NASDAQ", "NEW YORK STOCK EXCHANGE, INC." -> "NYSE".</summary>
        public static string ShortExchange(string name) {
            if (name == null || name.Length == 0) return "";
            var upper = name.ToUpper();
            if (upper.IndexOf("NASDAQ") >= 0) return "NASDAQ";
            if (upper.IndexOf("ARCA") >= 0) return "NYSE Arca";
            if (upper.IndexOf("NYSE MKT") >= 0 || upper.IndexOf("AMERICAN") >= 0) return "NYSE American";
            if (upper.IndexOf("NEW YORK") >= 0 || upper.IndexOf("NYSE") >= 0) return "NYSE";
            if (upper.IndexOf("CBOE") >= 0 || upper.IndexOf("BATS") >= 0) return "Cboe";
            var cut = name.IndexOf(' ');
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>From /stock/market-status: open flag and session ("pre-market", "regular", "post-market" or null).</summary>
        public static bool ParseMarket(string json, out string session, out string holiday) {
            var r = Json.Parse(json);
            session = Json.Str(r, "session");
            holiday = Json.Str(r, "holiday");
            return Json.Bool(r, "isOpen");
        }
    }
}
