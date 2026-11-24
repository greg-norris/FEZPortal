using System;
using System.Collections;

namespace PortalDesktop.Data {
    public enum WifiStatus { Off, Connecting, Connected, Failed }

    /// <summary>
    /// Everything the screens show. The background data service writes it (inside lock(state)),
    /// the UI thread reads it while drawing (also inside lock(state)). Each area has a version
    /// number that goes up on every change, so screens know when to redraw.
    /// </summary>
    public class PortalState {
        public Config Config = Config.Defaults();

        // Storage
        public bool SdPresent;
        public string SdMessage = "No SD card";

        // Wi-Fi
        public WifiStatus Wifi = WifiStatus.Off;
        public string WifiMessage = "Wi-Fi off";
        public string IpAddress = "";
        public int SignalStrength;          // RSSI in dBm, 0 = unknown
        public string WifiSsid = "";        // network joined (or being joined)
        public string[] WifiScan = new string[0];   // network names seen in the last scan
        public bool WifiScanning;
        public string WifiScanMessage = ""; // e.g. a scan error

        // Time
        public bool TimeSynced;
        public DateTime LastTimeSync;       // UTC
        public string TimeZone = "";        // e.g. "America/Detroit"

        // Location and weather
        public Location Location;
        public WeatherData Weather;
        public string WeatherMessage = "Waiting for Wi-Fi";

        // Stocks
        public StockItem[] Stocks = new StockItem[0];
        public bool HasMarketStatus;
        public bool MarketOpen;
        public string MarketSession;
        public string MarketHoliday;
        public string StocksMessage = "Waiting for Wi-Fi";
        public DateTime StocksUpdated;      // UTC, MinValue = never

        // News (news.txt)
        public NewsItem[] News = new NewsItem[0];
        public string NewsMessage = "Waiting for Wi-Fi";
        public DateTime NewsUpdated;        // UTC of the last good update, MinValue = never

        // Online calendars (ics= in calendar.txt), already expanded into local-time events.
        public ArrayList OnlineEvents = new ArrayList();
        public string CalendarMessage = "";         // e.g. a download error
        public bool CalendarSyncing;                // a download is in progress
        public DateTime CalendarUpdated;            // UTC of the last good download, MinValue = never

        ArrayList mergedEvents, mergedLocal, mergedOnline;

        /// <summary>Events from calendar.txt plus the online calendars (rebuilt when either list is replaced).</summary>
        public ArrayList Events {
            get {
                if (this.mergedEvents == null || this.mergedLocal != this.Config.Events || this.mergedOnline != this.OnlineEvents) {
                    var all = new ArrayList();
                    foreach (var e in this.Config.Events) all.Add(e);
                    foreach (var e in this.OnlineEvents) all.Add(e);
                    this.mergedEvents = all;
                    this.mergedLocal = this.Config.Events;
                    this.mergedOnline = this.OnlineEvents;
                }
                return this.mergedEvents;
            }
        }

        // Clock tools (the alarm itself is a setting: Config.Alarm)
        public CountdownTimer Countdown = new CountdownTimer();
        public LapTimer Stopwatch = new LapTimer();

        // A ringing countdown or alarm, shown over every screen until dismissed.
        public int AlertKind;               // AlertNone, AlertCountdown or AlertAlarm
        public string AlertTitle = "", AlertDetail = "";
        public const int AlertNone = 0, AlertCountdown = 1, AlertAlarm = 2;

        // Change counters
        public int StatusVersion;           // SD, Wi-Fi, signal (status bar)
        public int TimeVersion;             // time sync, time zone
        public int WeatherVersion;          // weather, location
        public int StocksVersion;           // quotes, names
        public int MarketVersion;           // market open/closed
        public int CalendarVersion;
        public int WifiVersion;             // scan results, saved networks, auto-join
        public int NewsVersion;             // headlines, news message

        public void SetStocksFromConfig() {
            var list = new StockItem[this.Config.Symbols.Length];
            for (var i = 0; i < list.Length; i++) list[i] = new StockItem(this.Config.Symbols[i]);
            this.Stocks = list;
            this.StocksVersion++;
        }

        /// <summary>
        /// Rebuilds the watchlist after Config.Symbols changed on the Portal. Symbols that stay keep their
        /// item (quote, chart samples, company details), so only a new symbol starts out empty.
        /// </summary>
        public void SetStocksFromSymbols() {
            var symbols = this.Config.Symbols;
            var list = new StockItem[symbols.Length];
            for (var i = 0; i < list.Length; i++) list[i] = this.FindStock(symbols[i]) ?? new StockItem(symbols[i]);
            this.Stocks = list;
            this.StocksVersion++;
        }

        public StockItem FindStock(string symbol) {
            foreach (var s in this.Stocks) {
                if (s.Symbol == symbol) return s;
            }
            return null;
        }
    }

    /// <summary>What the screens can ask of the device (the desktop preview passes a stand-in).</summary>
    public interface IPortalHost {
        /// <summary>Writes a settings file in the Portal folder on the SD card (false if there is no card).</summary>
        bool SaveSettings(string fileName, string text);

        void RefreshWeather();

        void RefreshStocks();

        /// <summary>Downloads the online calendars again as soon as possible.</summary>
        void RefreshCalendar();

        /// <summary>Gets the latest headlines as soon as possible.</summary>
        void RefreshNews();

        /// <summary>Looks for Wi-Fi networks in range (results land in WifiScan).</summary>
        void ScanWifi();

        /// <summary>Joins a network; once connected it is saved at the top of wifi.txt.</summary>
        void JoinWifi(string ssid, string password);

        /// <summary>Starts (true) or stops (false) the alarm sound; it stops by itself after a minute.</summary>
        void Ring(bool on);

        /// <summary>One short burst of the alarm sound, to check it can be heard.</summary>
        void Chirp();
    }
}
