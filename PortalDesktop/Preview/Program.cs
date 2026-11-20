using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PortalDesktop.App;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.Preview {
    /// <summary>
    /// Renders every Portal Desktop screen to PNGs on the PC, using the same drawing code and
    /// fonts as the board. Sample API answers in samples\ also exercise the JSON parsers.
    /// </summary>
    static class Program {
        class FakeHost : IPortalHost {
            public bool SaveSettings(string fileName, string text) {
                Console.WriteLine("(would save " + fileName + ")");
                return true;
            }
            public void RefreshWeather() { }
            public void RefreshStocks() { }
            public void ScanWifi() { }
            public void RefreshCalendar() => Console.WriteLine("(would sync the calendar)");
            public void Ring(bool on) => Console.WriteLine(on ? "(ringing)" : "(ringing stopped)");
            public void Chirp() => Console.WriteLine("(chirp)");
            public void JoinWifi(string ssid, string password) => Console.WriteLine("(would join " + ssid + " with password " + password + ")");
        }

        static string outDir;

        static int Main(string[] args) {
            if (args.Length != 3) {
                Console.Error.WriteLine("usage: PortalPreview <font dir> <samples dir> <out dir>");
                return 1;
            }
            var fontDir = args[0];
            var samples = args[1];
            outDir = args[2];
            Directory.CreateDirectory(outDir);

            // Monday 16 Nov 2026, 10:42:27 in Detroit (UTC-5).
            Clock.OffsetSeconds = -5 * 3600;
            Clock.Freeze(new DateTime(2026, 11, 16, 15, 42, 27));

            var canvas = new Canvas(Theme.ScreenWidth, Theme.ScreenHeight, fontDir);

            // A first boot: no card, no Wi-Fi yet.
            var empty = new PortalState();
            empty.SetStocksFromConfig();
            empty.Wifi = WifiStatus.Connecting;
            empty.WifiMessage = "Connecting to Hacked...";
            var bootShell = new Shell(empty, new FakeHost(), canvas);
            Render(bootShell, bootShell.Home, canvas, "boot-home");
            Render(bootShell, bootShell.WeatherApp, canvas, "boot-weather");
            Render(bootShell, bootShell.StocksApp, canvas, "boot-stocks");
            empty.WifiScanning = false;
            empty.WifiScanMessage = "Scan failed: module not ready";
            Render(bootShell, bootShell.WifiApp, canvas, "boot-wifi");

            var state = SampleState(samples);
            var shell = new Shell(state, new FakeHost(), canvas);
            Render(shell, shell.Home, canvas, "home");
            Render(shell, shell.ClockApp, canvas, "clock");
            shell.Show(shell.Home);
            shell.Swipe(-1);                     // swipe right on the desktop
            shell.Draw(Clock.Now);
            Save(canvas, "clock-face");
            Console.WriteLine("swipe on the desktop opens: " + shell.Current.GetType().Name);
            shell.Tap(20, 15);                   // where the back arrow would be: outside the clock, nothing happens
            Console.WriteLine("tap outside the clock stays on: " + shell.Current.GetType().Name);
            shell.Tap(240, 136);                 // the clock
            Console.WriteLine("tap on the clock returns to: " + shell.Current.GetType().Name);
            shell.Show(shell.ClockApp);
            for (var i = 1; i <= 3; i++) {
                shell.Tap(130, 152);              // tap the clock: next style
                shell.Draw(Clock.Now);
                Save(canvas, "clock-" + SevenSegment.FaceName(state.Config.ClockFace).ToLower());
            }
            state.Config.Alarm.Enabled = true;
            shell.Draw(Clock.Now);
            Save(canvas, "clock-blue-alarm");
            shell.Show(shell.FaceApp);
            shell.Draw(Clock.Now);
            Save(canvas, "clock-face-blue");
            state.Config.Use24Hour = true;
            state.Config.ClockFace = SevenSegment.Green;
            shell.Draw(Clock.Now);
            Save(canvas, "clock-face-green-24h");
            state.Config.Alarm.Enabled = false;
            state.Config.Use24Hour = false;
            shell.Show(shell.ClockApp);
            shell.Tap(130, 152);                  // green -> blue
            shell.Tap(130, 152);                  // blue -> analog
            Console.WriteLine("clock style after a full cycle: " + SevenSegment.FaceName(state.Config.ClockFace));

            // Clock tools: buttons on the clock page open each one.
            var utcNow = Clock.UtcNow;
            shell.Tap(298, 146 + 30);            // Countdown button
            shell.Draw(Clock.Now);
            Save(canvas, "countdown");
            shell.Tap(72 + 20, 58);              // hours up
            shell.Tap(394, 78);                  // Start
            Clock.Freeze(utcNow.AddSeconds(1500.4));
            shell.Draw(Clock.Now);
            Save(canvas, "countdown-running");
            shell.Tap(20, 15);                   // back -> clock page
            Console.WriteLine("back from Countdown returns to: " + shell.Current.Title);
            state.Stopwatch.Start(utcNow.AddSeconds(1000));
            foreach (var t in new[] { 1061.3, 1119.9, 1185.2, 1240.7 }) state.Stopwatch.Lap(utcNow.AddSeconds(t));
            state.Config.Alarm.Enabled = true;
            state.Config.Alarm.Hour = 6;
            state.Config.Alarm.Minute = 45;
            shell.Draw(Clock.Now);
            Save(canvas, "clock-tools");
            shell.Tap(298 + 64, 146 + 30);       // Timer button
            shell.Draw(Clock.Now);
            Save(canvas, "timer");
            shell.Tap(20, 15);
            shell.Tap(298 + 128, 146 + 30);      // Alarm button
            shell.Draw(Clock.Now);
            Save(canvas, "alarm");
            Render(shell, shell.Home, canvas, "home-alarm");
            shell.Tap(185, 38 + 58);             // the bell under AM/PM
            Console.WriteLine("bell opens: " + shell.Current.Title);
            shell.Tap(20, 15);
            Console.WriteLine("back from there returns to: " + shell.Current.Title);
            Console.WriteLine("clock.txt would be:\n" + state.Config.ClockText());

            // The countdown runs out on another screen: the pop-up rings over it.
            shell.Show(shell.WeatherApp);
            Clock.Freeze(utcNow.AddSeconds(3905));
            shell.Update();
            Save(canvas, "alert-countdown");
            shell.Tap(240, 54 + 186 - 40);       // Stop
            state.Config.Alarm.Hour = 10;
            state.Config.Alarm.Minute = 42;
            state.Config.Alarm.Repeat = AlarmClock.EveryDay;
            Clock.Freeze(new DateTime(2026, 11, 16, 15, 42, 0));
            shell.Update();
            Save(canvas, "alert-alarm");
            shell.Tap(170, 54 + 186 - 40);       // Snooze
            Console.WriteLine("snoozed until " + state.Config.Alarm.SnoozeUntil + ", alert " + state.AlertKind);
            state.Countdown.Reset();
            state.Stopwatch.Reset();
            state.Config.Alarm = new AlarmClock();
            Clock.Freeze(utcNow);
            Render(shell, shell.CalendarApp, canvas, "calendar");
            shell.Tap(GridDayX(2026, 11, 17), GridDayY(2026, 11, 17));   // Tuesday: the Pacific-time meeting
            shell.Draw(Clock.Now);
            Save(canvas, "calendar-online");
            Console.WriteLine("calendar bar: " + shell.CalendarApp.BarText);
            shell.Tap(400, 104 + 10);            // the first event in the day list
            shell.Draw(Clock.Now);
            Save(canvas, "calendar-event");
            shell.Swipe(1);                      // next page of the message
            shell.Draw(Clock.Now);
            Save(canvas, "calendar-event-2");
            shell.Swipe(1);
            shell.Draw(Clock.Now);
            Save(canvas, "calendar-event-3");
            shell.Swipe(1);
            shell.Draw(Clock.Now);
            Save(canvas, "calendar-event-4");
            shell.Tap(20, 15);                   // back
            Console.WriteLine("back from the event returns to: " + shell.Current.Title + ", still on " +
                              shell.CalendarApp.GetType().GetField("selected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(shell.CalendarApp));
            Render(shell, shell.WeatherApp, canvas, "weather");

            // Calculator: checks, then a calculation left on screen.
            var calc = shell.CalculatorApp;
            foreach (var test in new[] {
                "2 + 3 \u00D7 4 =|14", "10 \u00F7 4 =|2.5", "0.1 + 0.2 =|0.3", "1 \u00F7 3 =|0.3333333333",
                "7 \u2212 10 =|-3", "5 + =|10", "9 \u00F7 0 =|Error", "1234567 \u00D7 1000 =|1,234,567,000",
                "50 % =|0.5", "3 +/- \u00D7 2 =|-6", "12 del 5 =|15", "2 \u00D7 \u2212 + 3 =|5" }) {
                var parts = test.Split('|');
                calc.Press("C");
                foreach (var key in parts[0].Split(' ')) {
                    if (key == "+/-" || key == "del" || key == "%" || key.Length == 1 && !char.IsDigit(key[0])) { calc.Press(key); continue; }
                    foreach (var ch in key) calc.Press(ch.ToString());
                }
                var shown = calc.Display.Replace("−", "-");
                Console.WriteLine((shown == parts[1] ? "ok   " : "FAIL ") + parts[0].Replace("\u00D7", "x").Replace("\u00F7", "/").Replace("\u2212", "-") + " -> " + shown);
            }
            calc.Press("C");
            foreach (var key in new[] { "1", "2", "5", "0", "\u00D7", "1", "2", "+", "7", "5" }) calc.Press(key);
            Render(shell, calc, canvas, "calculator");
            Render(shell, shell.StocksApp, canvas, "stocks");
            shell.Tap(60, 80);                   // first card: a morning of samples
            shell.Draw(Clock.Now);
            Save(canvas, "stock-detail");
            shell.Swipe(1);
            shell.Swipe(1);                      // third symbol: NVDA's real quote from Friday
            shell.Draw(Clock.Now);
            Save(canvas, "stock-detail-nochart");

            // Wi-Fi: opened from the status bar icon of the stocks app; back returns to the stock.
            shell.Tap(440, 15);
            state.WifiScanning = false;
            shell.Draw(Clock.Now);
            Save(canvas, "wifi");
            shell.Tap(440, 114 + 3 * 29 + 10);   // fourth row: a network that isn't saved -> keyboard
            foreach (var ch in new[] { 2, 3, 4 }) shell.Tap(28 + ch * 47, 74 + 10);   // e r t
            shell.Tap(20, 74 + 2 * 49 + 20);      // shift
            shell.Tap(240, 74 + 2 * 49 + 20);     // V
            shell.Draw(Clock.Now);
            Save(canvas, "wifi-password");
            shell.Tap(60, 74 + 3 * 49 + 20);      // 123
            shell.Tap(440, 40);                   // Show
            shell.Draw(Clock.Now);
            Save(canvas, "wifi-password-123");
            shell.Tap(400, 74 + 3 * 49 + 20);     // Join
            shell.Tap(20, 15);                    // back from Wi-Fi
            Console.WriteLine("back from Wi-Fi returns to: " + shell.Current.Title);

            // Watchlist editing: Remove on a stock's page (asks first), + on the watchlist (keyboard).
            shell.Draw(Clock.Now);
            Console.WriteLine("detail bar button: " + shell.Current.BarButton + " at x=" + shell.BarButtonX);
            shell.Draw(Clock.Now);
            shell.Tap(shell.BarButtonX, 15);
            shell.Draw(Clock.Now);
            Save(canvas, "stock-remove-ask");
            shell.Tap(170, 190);                  // Cancel
            Console.WriteLine("after Cancel the bar button is: " + shell.Current.BarButton);
            shell.Draw(Clock.Now);
            shell.Tap(shell.BarButtonX, 15);
            shell.Tap(310, 190);                  // Remove
            shell.Draw(Clock.Now);
            Save(canvas, "stock-removed");
            Console.WriteLine("symbols after removing: " + string.Join(",", state.Config.Symbols) + " (bar: " + shell.Current.BarText + ")");
            Console.WriteLine("watchlist bar button: " + shell.Current.BarButton + " at x=" + shell.BarButtonX);
            shell.Draw(Clock.Now);
            shell.Tap(shell.BarButtonX, 15);      // +
            shell.Tap(51, 145);                   // a
            shell.Tap(380, 194);                  // m
            shell.Tap(145, 145);                  // d
            shell.Draw(Clock.Now);
            Save(canvas, "stock-add");
            shell.Tap(406, 243);                  // Add
            shell.Draw(Clock.Now);
            Save(canvas, "stock-added");
            Console.WriteLine("symbols after adding: " + string.Join(",", state.Config.Symbols) + " (bar: " + shell.Current.BarText + ")");
            shell.Draw(Clock.Now);
            shell.Tap(shell.BarButtonX, 15);
            shell.Tap(192, 145);                  // f
            shell.Tap(406, 243);                  // Add
            Console.WriteLine("adding F again: " + shell.Current.Title + " / " + shell.Current.BarText);
            Console.WriteLine("stocks.txt would be:\n" + state.Config.StocksFile.ToText());
            foreach (var t in new[] { "brk.b", " msft ", "BINANCE:BTCUSDT", "AB CD", "^GSPC", "" }) {
                Console.WriteLine("CleanSymbol(\"" + t + "\") = " + (Config.CleanSymbol(t) ?? "null"));
            }
            shell.Show(shell.StocksApp);

            // Watchlist paging: slide up for the second page, down to go back; the status bar no longer turns pages.
            var keep = state.Config.Symbols;
            state.Config.SetSymbols(new[] { "OKLO", "NCLH", "NVDA", "F", "GOOGL", "AMC", "PFE", "GNS", "AMD", "MSFT" });
            state.SetStocksFromSymbols();
            shell.Show(shell.StocksApp);
            shell.Tap(250, 15);                   // the status text in the middle of the bar
            Console.WriteLine("stocks after tapping the status bar: " + shell.Current.Title + " / " + shell.Current.BarText);
            shell.VerticalSwipe(-1);
            Console.WriteLine("stocks after sliding down on page 1: " + shell.Current.BarText);
            shell.VerticalSwipe(1);
            shell.Draw(Clock.Now);
            Save(canvas, "stocks-page2");
            Console.WriteLine("stocks after sliding up: " + shell.Current.BarText);
            shell.VerticalSwipe(1);
            Console.WriteLine("stocks after sliding up on the last page: " + shell.Current.BarText);
            shell.VerticalSwipe(-1);
            Console.WriteLine("stocks after sliding down: " + shell.Current.BarText);
            state.Config.SetSymbols(keep);
            state.SetStocksFromSymbols();

            // Settings: opened from "FEZ Portal Desktop" on the desktop; switch to the dark theme.
            shell.Show(shell.Home);
            shell.Tap(100, 15);
            shell.Draw(Clock.Now);
            Save(canvas, "settings");
            shell.Tap(360, 120);                  // Dark tile
            shell.Draw(Clock.Now);
            Save(canvas, "settings-dark");
            Render(shell, shell.Home, canvas, "dark-home");
            Render(shell, shell.WeatherApp, canvas, "dark-weather");
            Render(shell, shell.ClockApp, canvas, "dark-clock");
            Render(shell, shell.CalendarApp, canvas, "dark-calendar");
            Console.WriteLine("display.txt would be:" + state.Config.DisplayFile.ToText());
            Console.WriteLine("wifi.txt would be:\n" + state.Config.WifiText());

            // Parser checks
            var unknown = Quote.Parse(File.ReadAllText(Path.Combine(samples, "unknown.json")));
            Console.WriteLine("unknown symbol parses to null: " + (unknown == null));
            string session, holiday;
            var open = Finnhub.ParseMarket(File.ReadAllText(Path.Combine(samples, "market.json")), out session, out holiday);
            Console.WriteLine("market open=" + open + " session=" + session + " holiday=" + (holiday ?? "null"));
            var profile = Profile.Parse(File.ReadAllText(Path.Combine(samples, "profile.json")));
            Console.WriteLine("profile: " + profile.ToText() + " -> " + Profile.FromText(profile.ToText()).ToText());
            var metrics = Metrics.Parse(File.ReadAllText(Path.Combine(samples, "metric.json")));
            Console.WriteLine("metrics: 52w " + metrics.Low52 + "-" + metrics.High52 + " pe=" + metrics.PE + " yield=" + metrics.DividendYield);
            var cut = Metrics.Parse("{\"metric\":{\"52WeekHigh\":212.19,\"52WeekLow\":86.6");
            Console.WriteLine("cut-off metrics: 52w " + cut.Low52 + "-" + cut.High52 + " (high only expected)");
            Console.WriteLine("market text: " + Market.StatusText(state, Clock.UtcNow) + ", EDT offset in July: " + Market.EasternOffsetSeconds(new DateTime(2026, 7, 1)) / 3600);
            Console.WriteLine("weather url: " + WeatherData.Url(42.3314, -83.0458, false));
            return 0;
        }

        static PortalState SampleState(string samples) {
            var s = new PortalState();
            s.Config = Config.Defaults();
            s.Config.Apply(DefaultFiles.CalendarName, DefaultFiles.Calendar +
                "2026-11-16 9:00 Stand-up meeting\n" +
                "2026-11-16 12:30 Lunch with Sam\n" +
                "2026-11-16 15:00 Portal demo\n" +
                "2026-11-20 Team offsite\n" +
                "2026-11-26 Thanksgiving\n" +
                "2026-11-27 8:00 Black Friday sale\n");
            s.SetStocksFromConfig();
            s.SdPresent = true;
            s.Wifi = WifiStatus.Connected;
            s.WifiMessage = "Connected to Hacked";
            s.WifiSsid = "Hacked";
            s.Config.RememberWifi("Office-Guest", "welcome2026");
            s.Config.RememberWifi("Hacked", "hackme123");
            s.Config.Apply(DefaultFiles.WifiName, s.Config.WifiText() + "ssid3=Cabin\r\npassword3=lakehouse\r\n");
            s.WifiScan = new[] { "Hacked", "NETGEAR42-5G", "Office-Guest", "xfinitywifi", "DIRECT-7B-HP OfficeJet 8020", "Neighbors Network" };
            s.IpAddress = "192.168.43.17";
            s.SignalStrength = -58;
            s.TimeSynced = true;
            s.LastTimeSync = new DateTime(2026, 11, 16, 15, 30, 4);
            s.TimeZone = "America/Detroit";
            s.Location = new Location { Place = "Detroit, Michigan", Latitude = 42.3314, Longitude = -83.0458 };
            // An online calendar (ics= line), read the way the Portal streams it.
            s.Config.CalendarUrls.Add("https://example.com/calendar.ics");
            var feed = new Ics();
            foreach (var line in File.ReadAllText(Path.Combine(samples, "calendar.ics")).Split('\n')) {
                var l = line.TrimEnd('\r');
                if (l.Length > 0 && l[0] == ' ' && feed.SkipFolded) continue;
                feed.Line(l);
            }
            feed.Finish();
            var nowLocal = Clock.Now;
            s.OnlineEvents = feed.Expand(new DateTime(nowLocal.Year, nowLocal.Month, 1).AddDays(-45), nowLocal.Date.AddDays(400), Clock.UtcNow, Clock.OffsetSeconds);
            s.CalendarUpdated = new DateTime(2026, 11, 16, 15, 30, 0);
            Console.WriteLine("online calendar: " + feed.EventCount + " events -> " + s.OnlineEvents.Count + " in range");
            foreach (CalendarEvent e in s.OnlineEvents) {
                if (e.Year == 2026 && e.Month == 11 && e.Day >= 16 && e.Day <= 27)
                    Console.WriteLine("  " + e.Month + "/" + e.Day + " " + (e.Hour < 0 ? "all day" : e.Hour + ":" + Format.Two(e.Minute)) + " " + e.Text);
            }
            s.Weather = WeatherData.Parse(File.ReadAllText(Path.Combine(samples, "weather.json")));
            s.HasMarketStatus = true;
            s.MarketOpen = true;
            s.MarketSession = "regular";

            // Real NVDA quote, then made-up ones for the rest, each with a morning of samples.
            var names = new[] { "Oklo Inc", "Norwegian Cruise Line Holdings Ltd", "NVIDIA Corporation", "Ford Motor Co",
                                "Alphabet Inc", "AMC Entertainment Holdings Inc", "Pfizer Inc", "Genius Group Ltd" };
            var prices = new[] { 71.18, 21.63, 162.85, 13.42, 284.10, 2.87, 25.31, 0.3412 };
            var changes = new[] { 4.12, -0.48, -2.0097, 0.85, 1.12, -3.36, 0.22, 7.9 };
            var rnd = new Random(7);
            for (var i = 0; i < s.Stocks.Length; i++) {
                var item = s.Stocks[i];
                item.Name = names[i];
                var close = prices[i] / (1 + changes[i] / 100);
                var price = close * (1 + rnd.NextDouble() * 0.01 - 0.005);
                var t0 = 1794839400L; // 9:30 ET
                for (var k = 0; k <= 72; k++) {
                    var target = close + (prices[i] - close) * k / 72.0;
                    price = price + (target - price) * 0.25 + close * (rnd.NextDouble() - 0.5) * 0.006;
                    if (k == 72) price = prices[i];
                    var q = Quote.Parse("{\"c\":" + Num(price) + ",\"d\":" + Num(price - close) + ",\"dp\":" + Num((price - close) / close * 100) +
                                        ",\"h\":" + Num(Math.Max(price, close * 1.01)) + ",\"l\":" + Num(Math.Min(price, close * 0.99)) +
                                        ",\"o\":" + Num(close * 1.002) + ",\"pc\":" + Num(close) + ",\"t\":" + (t0 + k * 60) + "}");
                    item.Update(q);
                }
            }
            s.FindStock("NVDA").Update(Quote.Parse(File.ReadAllText(Path.Combine(samples, "quote.json"))));

            // Company details and metrics: NVDA's are real answers, the rest made up.
            var exchanges = new[] { "NYSE", "NYSE", "NASDAQ", "NYSE", "NASDAQ", "NYSE", "NYSE", "NYSE American" };
            var industries = new[] { "Utilities", "Hotels, Restaurants & Leisure", "", "Automobiles", "Media", "Media", "Pharmaceuticals", "Diversified Consumer Services" };
            var shares = new[] { 147.9, 451.2, 0, 3990.0, 12100.0, 431.5, 5670.0, 0 };
            var lows = new[] { 18.54, 14.21, 0, 9.49, 140.53, 2.45, 22.62, 0 };
            var highs = new[] { 115.72, 29.29, 0, 14.80, 291.59, 5.56, 31.54, 0 };
            var pes = new[] { -1, 12.4, 0, 11.6, 27.2, -1, 18.3, -1 };
            var yields = new[] { 0, 0, 0, 5.07, 0.29, 0, 6.87, 0 };
            for (var i = 0; i < s.Stocks.Length; i++) {
                var item = s.Stocks[i];
                item.SetProfile(new Profile { Name = names[i], Exchange = exchanges[i], Industry = industries[i], Shares = shares[i] });
                item.SetMetrics(new Metrics { Low52 = lows[i], High52 = highs[i], PE = pes[i], DividendYield = yields[i] });
            }
            var nvda = s.FindStock("NVDA");
            nvda.SetProfile(Profile.Parse(File.ReadAllText(Path.Combine(samples, "profile.json"))));
            nvda.SetMetrics(Metrics.Parse(File.ReadAllText(Path.Combine(samples, "metric.json"))));
            s.FindStock("GNS").Error = null;
            return s;
        }

        // Calendar grid cell centre (matches CalendarScreen's layout).
        static int GridDayX(int y, int m, int d) => 18 + ((int)new DateTime(y, m, 1).DayOfWeek + d - 1) % 7 * 40 + 20;
        static int GridDayY(int y, int m, int d) => 88 + ((int)new DateTime(y, m, 1).DayOfWeek + d - 1) / 7 * 29 + 14;

        static string Num(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        static void Render(Shell shell, Screen screen, Canvas canvas, string name) {
            shell.Show(screen);
            shell.Draw(Clock.Now);
            Save(canvas, name);
        }

        static void Save(Canvas canvas, string name) {
            var path = Path.Combine(outDir, name + ".png");
            canvas.Bitmap.Save(path, ImageFormat.Png);
            using (var zoom = new Bitmap(960, 544))
            using (var g = Graphics.FromImage(zoom)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(canvas.Bitmap, 0, 0, 960, 544);
                zoom.Save(Path.Combine(outDir, name + ".2x.png"), ImageFormat.Png);
            }
            Console.WriteLine("Wrote " + path);
        }
    }
}
