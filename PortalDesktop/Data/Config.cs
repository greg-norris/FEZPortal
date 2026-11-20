using System.Collections;

namespace PortalDesktop.Data {
    /// <summary>Settings from the text files in the Portal folder on the SD card.</summary>
    public class Config {
        public const int MaxSymbols = 16;
        public const int MaxWifiNetworks = 8;

        // wifi.txt: saved networks, most recently used first. Replaced (never changed) on edits,
        // so the data thread can hold on to a list while the UI saves a new one.
        public ArrayList WifiNetworks = new ArrayList();
        public bool WifiAutoJoin = true;

        // clock.txt
        // display.txt
        public SettingsFile DisplayFile = new SettingsFile(DefaultFiles.Display);
        public bool DarkTheme;

        // clock.txt
        public SettingsFile ClockFile = new SettingsFile(DefaultFiles.Clock);
        public bool OffsetAuto = true;
        public int FixedOffsetSeconds;
        public bool Use24Hour;
        public int CountdownSeconds = 300;
        public int ClockFace;               // 0 analog, 1 red, 2 green, 3 blue 7-segment (App.SevenSegment)
        public AlarmClock Alarm = new AlarmClock();
        public string NtpServer = "pool.ntp.org";

        // weather.txt
        public bool LocationAuto = true;
        public string PlaceName = "";
        public double Latitude;
        public double Longitude;
        public bool Metric;

        // stocks.txt
        public SettingsFile StocksFile = new SettingsFile(DefaultFiles.Stocks);
        public string FinnhubKey = "";
        public string[] Symbols = new string[0];

        // calendar.txt
        public ArrayList Events = new ArrayList();
        public ArrayList CalendarUrls = new ArrayList();   // ics= lines: online calendars to show too

        /// <summary>Settings from the built-in default files (used when there is no SD card).</summary>
        public static Config Defaults() {
            var config = new Config();
            for (var i = 0; i < DefaultFiles.Names.Length; i++) config.Apply(DefaultFiles.Names[i], DefaultFiles.Contents[i]);
            return config;
        }

        /// <summary>Reads one settings file (by its file name, e.g. "wifi.txt").</summary>
        public void Apply(string fileName, string text) {
            if (fileName == DefaultFiles.CalendarName) {
                this.Events = CalendarEvents.Parse(text);
                this.CalendarUrls = CalendarEvents.Urls(text);
                return;
            }

            var f = new SettingsFile(text);
            if (fileName == DefaultFiles.WifiName) {
                // ssid/password, then ssid2/password2, ssid3/password3...
                var list = new ArrayList();
                for (var i = 1; i <= MaxWifiNetworks; i++) {
                    var suffix = i == 1 ? "" : i.ToString();
                    var ssid = f.Get("ssid" + suffix, "");
                    if (ssid.Length == 0 || Find(list, ssid) != null) continue;
                    list.Add(new WifiNetwork(ssid, f.Get("password" + suffix, "")));
                }
                this.WifiNetworks = list;
                this.WifiAutoJoin = f.Get("autojoin", "yes").ToLower() != "no";
            }
            else if (fileName == DefaultFiles.DisplayName) {
                this.DisplayFile = f;
                this.DarkTheme = f.Get("theme", "light").ToLower() == "dark";
            }
            else if (fileName == DefaultFiles.ClockName) {
                this.ClockFile = f;
                var offset = f.Get("utcoffset", "auto").ToLower();
                this.OffsetAuto = offset == "auto" || offset.Length == 0;
                this.FixedOffsetSeconds = this.OffsetAuto ? 0 : (int)(f.GetNumber("utcoffset", 0) * 3600);
                this.Use24Hour = f.Get("format", "12") == "24";
                this.NtpServer = f.Get("ntpserver", "pool.ntp.org");

                var face = f.Get("face", "analog").ToLower();
                this.ClockFace = face == "red" ? 1 : face == "green" ? 2 : face == "blue" ? 3 : 0;

                var countdown = (int)f.GetNumber("countdown", 300);
                this.CountdownSeconds = countdown > 0 && countdown < 24 * 3600 ? countdown : 300;
                var alarm = new AlarmClock();
                var time = f.Get("alarm", "7:00");
                var colon = time.IndexOf(':');
                int hour, minute;
                if (colon > 0 && Clock.Digits(time, 0, colon, out hour) && Clock.Digits(time, colon + 1, time.Length - colon - 1, out minute) &&
                    hour < 24 && minute < 60) {
                    alarm.Hour = hour;
                    alarm.Minute = minute;
                }
                alarm.Enabled = f.Get("alarmon", "no").ToLower() == "yes";
                var repeat = f.Get("alarmrepeat", "daily").ToLower();
                alarm.Repeat = repeat == "weekdays" ? AlarmClock.Weekdays : repeat == "once" ? AlarmClock.Once : AlarmClock.EveryDay;
                this.Alarm = alarm;
            }
            else if (fileName == DefaultFiles.WeatherName) {
                var location = f.Get("location", "auto");
                this.Latitude = f.GetNumber("latitude", 0);
                this.Longitude = f.GetNumber("longitude", 0);
                this.LocationAuto = location.ToLower() == "auto" || (this.Latitude == 0 && this.Longitude == 0);
                this.PlaceName = this.LocationAuto ? "" : location;
                this.Metric = f.Get("units", "f").ToLower() == "c";
            }
            else if (fileName == DefaultFiles.StocksName) {
                this.StocksFile = f;
                this.FinnhubKey = f.Get("apikey", "");
                var list = new ArrayList();
                foreach (var part in f.Get("symbols", "").Split(',', ' ', ';')) {
                    var symbol = part.Trim().ToUpper();
                    if (symbol.Length == 0 || list.Contains(symbol) || list.Count >= MaxSymbols) continue;
                    list.Add(symbol);
                }
                this.Symbols = (string[])list.ToArray(typeof(string));
            }
        }

        // -- Wi-Fi networks ---------------------------------------------------------

        public WifiNetwork FindWifi(string ssid) => Find(this.WifiNetworks, ssid);

        static WifiNetwork Find(ArrayList list, string ssid) {
            foreach (WifiNetwork n in list) {
                if (n.Ssid == ssid) return n;
            }
            return null;
        }

        /// <summary>Puts a network at the top of the saved list. Returns true when the list changed.</summary>
        public bool RememberWifi(string ssid, string password) {
            var first = this.WifiNetworks.Count > 0 ? (WifiNetwork)this.WifiNetworks[0] : null;
            if (first != null && first.Ssid == ssid && first.Password == password) return false;
            var list = new ArrayList();
            list.Add(new WifiNetwork(ssid, password));
            foreach (WifiNetwork n in this.WifiNetworks) {
                if (n.Ssid != ssid && list.Count < MaxWifiNetworks) list.Add(n);
            }
            this.WifiNetworks = list;
            return true;
        }

        public void ForgetWifi(string ssid) {
            var list = new ArrayList();
            foreach (WifiNetwork n in this.WifiNetworks) {
                if (n.Ssid != ssid) list.Add(n);
            }
            this.WifiNetworks = list;
        }

        /// <summary>The wifi.txt text for the saved networks and the auto-join setting.</summary>
        public string WifiText() {
            var sb = new System.Text.StringBuilder();
            sb.Append(DefaultFiles.WifiHeader);
            for (var i = 0; i < this.WifiNetworks.Count; i++) {
                var n = (WifiNetwork)this.WifiNetworks[i];
                var suffix = i == 0 ? "" : (i + 1).ToString();
                sb.Append("ssid").Append(suffix).Append("=").Append(n.Ssid).Append("\r\n");
                sb.Append("password").Append(suffix).Append("=").Append(n.Password).Append("\r\n");
            }
            sb.Append(DefaultFiles.WifiAutoJoinComment);
            sb.Append("autojoin=").Append(this.WifiAutoJoin ? "yes" : "no").Append("\r\n");
            return sb.ToString();
        }

        // -- Watchlist ---------------------------------------------------------------

        public const int MaxSymbolLength = 20;

        /// <summary>
        /// A ticker typed on the Portal in the form Finnhub wants it ("brk.b" -> "BRK.B"), or null when it
        /// has characters a symbol can't have. Letters, digits, '.', '-' and ':' (as in BINANCE:BTCUSDT).
        /// </summary>
        public static string CleanSymbol(string text) {
            if (text == null) return null;
            var symbol = text.Trim().ToUpper();
            if (symbol.Length == 0 || symbol.Length > MaxSymbolLength) return null;
            // Indexed loop: foreach over a string doesn't compile against the TinyCLR mscorlib (CS7038).
            for (var i = 0; i < symbol.Length; i++) {
                var c = symbol[i];
                if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '.' && c != '-' && c != ':') return null;
            }
            return symbol;
        }

        public bool HasSymbol(string symbol) {
            foreach (var s in this.Symbols) {
                if (s == symbol) return true;
            }
            return false;
        }

        /// <summary>Replaces the watchlist and returns the new stocks.txt text to save (comments kept).</summary>
        public string SetSymbols(string[] symbols) {
            this.Symbols = symbols;
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < symbols.Length; i++) {
                if (i > 0) sb.Append(',');
                sb.Append(symbols[i]);
            }
            this.StocksFile.Set("symbols", sb.ToString());
            return this.StocksFile.ToText();
        }

        /// <summary>Switches the theme and returns the new display.txt text to save.</summary>
        public string SetDarkTheme(bool value) {
            this.DarkTheme = value;
            this.DisplayFile.Set("theme", value ? "dark" : "light");
            return this.DisplayFile.ToText();
        }

        /// <summary>The clock.txt text with the current countdown length and alarm (to save after a change).</summary>
        public string ClockText() {
            var a = this.Alarm;
            this.ClockFile.Set("face", this.ClockFace == 1 ? "red" : this.ClockFace == 2 ? "green" : this.ClockFace == 3 ? "blue" : "analog");
            this.ClockFile.Set("countdown", this.CountdownSeconds.ToString());
            this.ClockFile.Set("alarm", a.Hour + ":" + (a.Minute < 10 ? "0" : "") + a.Minute);
            this.ClockFile.Set("alarmon", a.Enabled ? "yes" : "no");
            this.ClockFile.Set("alarmrepeat", a.Repeat == AlarmClock.Weekdays ? "weekdays" : a.Repeat == AlarmClock.Once ? "once" : "daily");
            return this.ClockFile.ToText();
        }

        /// <summary>Switches the clock format and returns the new clock.txt text to save.</summary>
        public string SetUse24Hour(bool value) {
            this.Use24Hour = value;
            this.ClockFile.Set("format", value ? "24" : "12");
            return this.ClockFile.ToText();
        }
    }

    /// <summary>A saved Wi-Fi network.</summary>
    public class WifiNetwork {
        public readonly string Ssid;
        public readonly string Password;

        public WifiNetwork(string ssid, string password) {
            this.Ssid = ssid;
            this.Password = password ?? "";
        }
    }
}
