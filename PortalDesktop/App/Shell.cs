using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// The desktop shell: owns the screens, draws the status bar, routes touches and decides
    /// what needs redrawing. <see cref="Update"/> is called in a loop on the UI thread.
    /// </summary>
    public class Shell {
        public readonly PortalState State;
        public readonly IPortalHost Host;
        readonly Canvas canvas;

        public readonly HomeScreen Home;
        public readonly ClockScreen ClockApp;
        public readonly CalendarScreen CalendarApp;
        public readonly WeatherScreen WeatherApp;
        public readonly StocksScreen StocksApp;
        public readonly WifiScreen WifiApp;
        public readonly SettingsScreen SettingsApp;
        public readonly CountdownScreen CountdownApp;
        public readonly TimerScreen TimerApp;
        public readonly AlarmScreen AlarmApp;
        public readonly FaceScreen FaceApp;
        public readonly CalculatorScreen CalculatorApp;
        public readonly EventScreen EventApp;

        // "FEZ Portal Desktop" in the status bar of the desktop opens the settings.
        const int SettingsTapRight = 195;

        // Status bar areas that open the Wi-Fi screen: the network name on the desktop, the icons in apps.
        const int WifiTapLeftHome = 200, WifiTapLeftApp = 418;

        Screen current;
        Screen beforeWifi;      // where the back arrow on the Wi-Fi screen returns to
        bool dirty = true;
        int drawnStamp, drawnStatus;
        string drawnBarText;
        DateTime lastTick = DateTime.MinValue;
        int barButtonLeft, barButtonRight;   // where the current screen's status bar button was drawn (0, 0 = none)

        public Shell(PortalState state, IPortalHost host, Canvas canvas) {
            this.State = state;
            this.Host = host;
            this.canvas = canvas;
            this.Home = new HomeScreen(this);
            this.ClockApp = new ClockScreen(this);
            this.CalendarApp = new CalendarScreen(this);
            this.WeatherApp = new WeatherScreen(this);
            this.StocksApp = new StocksScreen(this);
            this.WifiApp = new WifiScreen(this);
            this.SettingsApp = new SettingsScreen(this);
            this.CountdownApp = new CountdownScreen(this);
            this.TimerApp = new TimerScreen(this);
            this.AlarmApp = new AlarmScreen(this);
            this.FaceApp = new FaceScreen(this);
            this.CalculatorApp = new CalculatorScreen(this);
            this.EventApp = new EventScreen(this);
            this.current = this.Home;
        }

        public Screen Current => this.current;

        /// <summary>Centre of the status bar button as last drawn (for the desktop preview), or 0 when there is none.</summary>
        public int BarButtonX => this.barButtonRight > 0 ? (this.barButtonLeft + this.barButtonRight) / 2 : 0;

        /// <summary>For screens that measure text while handling a tap.</summary>
        public Canvas Canvas => this.canvas;

        public void Show(Screen screen) {
            this.current = screen;
            screen.Opened();
            this.dirty = true;
        }

        /// <summary>Asks for a full redraw on the next update.</summary>
        public void Invalidate() => this.dirty = true;

        public void Tap(int x, int y) {
            lock (this.State) {
                // A ringing countdown or alarm takes every tap until it is dismissed.
                if (this.State.AlertKind != PortalState.AlertNone) {
                    this.AlertTap(x, y);
                    return;
                }
                // Status bar taps (full-screen pages have no status bar).
                var bar = y < Theme.BarHeight && !this.current.FullScreen;
                if (bar && this.current == this.Home && x < SettingsTapRight) {
                    this.Show(this.SettingsApp);
                    return;
                }
                if (bar && this.barButtonRight > 0 && this.current.BarButton != null &&
                    x >= this.barButtonLeft - 6 && x < this.barButtonRight + 6) {
                    this.current.BarButtonTap();
                    this.dirty = true;
                    return;
                }
                if (bar && this.current != this.WifiApp &&
                    x >= (this.current == this.Home ? WifiTapLeftHome : WifiTapLeftApp)) {
                    this.beforeWifi = this.current;
                    this.Show(this.WifiApp);
                    return;
                }
                if (bar && x < 170 && this.current != this.Home) {
                    if (this.current.Back()) {
                        this.dirty = true;
                    }
                    else if (this.current == this.WifiApp && this.beforeWifi != null) {
                        // Back to the app as it was left (not reopened from scratch).
                        this.current = this.beforeWifi;
                        this.beforeWifi = null;
                        this.dirty = true;
                    }
                    else {
                        // Back to the page this one came from, as it was left (Calendar keeps its
                        // day); to the desktop, which is always opened fresh.
                        var parent = this.current.BackTo;
                        if (parent != null) {
                            this.current = parent;
                            this.dirty = true;
                        }
                        else {
                            this.Show(this.Home);
                        }
                    }
                    return;
                }
                this.current.Tap(x, y);
            }
        }

        public void Swipe(int direction) {
            lock (this.State) {
                if (this.State.AlertKind == PortalState.AlertNone) this.current.Swipe(direction);
            }
        }

        public void VerticalSwipe(int direction) {
            lock (this.State) {
                if (this.State.AlertKind == PortalState.AlertNone) this.current.VerticalSwipe(direction);
            }
        }

        // -- Countdown and alarm --------------------------------------------------------

        /// <summary>Starts ringing when the countdown ends (checked every pass) or the alarm time comes (every second).</summary>
        void CheckAlerts(DateTime now, bool newSecond) {
            var s = this.State;
            if (s.Countdown.CheckDone(Clock.UtcNow)) {
                this.Raise(PortalState.AlertCountdown, "Time's up!", "The " + Format.Length(s.Config.CountdownSeconds) + " countdown has finished");
            }
            if (!newSecond) return;
            bool settingsChanged;
            if (s.Config.Alarm.Check(now, out settingsChanged)) {
                this.Raise(PortalState.AlertAlarm, "Alarm", Format.LongDate(now) + ", " + Format.ShortTime(now, s.Config.Use24Hour));
            }
            if (settingsChanged) this.Host.SaveSettings(DefaultFiles.ClockName, s.Config.ClockText());   // a one-time alarm turned off
        }

        void Raise(int kind, string title, string detail) {
            var s = this.State;
            s.AlertKind = kind;
            s.AlertTitle = title;
            s.AlertDetail = detail;
            this.Host.Ring(true);
            this.dirty = true;
        }

        void AlertTap(int x, int y) {
            var s = this.State;
            var action = AlertOverlay.Tap(s, x, y);
            if (action == AlertOverlay.None) return;
            if (action == AlertOverlay.Snooze) s.Config.Alarm.Snooze(Clock.Now);
            s.AlertKind = PortalState.AlertNone;
            this.Host.Ring(false);
            this.dirty = true;
        }

        /// <summary>Redraws what changed. Call often (every 20-50 ms).</summary>
        public void Update() {
            lock (this.State) {
                var now = Clock.Now;
                var c = this.canvas;
                var stamp = this.current.Stamp;
                var newSecond = now.Second != this.lastTick.Second || now.Minute != this.lastTick.Minute ||
                                now.Hour != this.lastTick.Hour || now.Day != this.lastTick.Day;
                if (now.Day != this.lastTick.Day || now.Month != this.lastTick.Month) this.dirty = true;
                this.CheckAlerts(now, newSecond);

                if (this.dirty || stamp != this.drawnStamp) {
                    this.Draw(now);
                    this.lastTick = now;
                    return;
                }

                // Screens build BarText from scratch, so only check it once a second (this loop runs
                // every ~30 ms and the strings would keep the garbage collector busy).
                var barChanged = newSecond && this.current.BarText != this.drawnBarText;
                if (!this.current.FullScreen &&
                    (this.State.StatusVersion != this.drawnStatus || barChanged || (newSecond && now.Minute != this.lastTick.Minute))) {
                    this.DrawStatusBar(c, now);
                    c.Flush(0, 0, Theme.ScreenWidth, Theme.BarHeight);
                }
                // Screens update parts of themselves; not while the pop-up covers them.
                var alert = this.State.AlertKind != PortalState.AlertNone;
                if (newSecond) {
                    if (!alert) this.current.Tick(c, now, this.lastTick);
                    this.lastTick = now;
                }
                if (!alert) this.current.Frame(c);
            }
        }

        /// <summary>Full redraw of the current screen (also used by the desktop preview).</summary>
        public void Draw(DateTime now) {
            var c = this.canvas;
            Theme.SetDark(this.State.Config.DarkTheme);
            if (this.current.FullScreen) {
                c.FillRect(Theme.Background, 0, 0, Theme.ScreenWidth, Theme.ScreenHeight);
                this.current.Draw(c);
            }
            else {
                c.FillRect(Theme.Background, 0, Theme.BarHeight, Theme.ScreenWidth, Theme.ScreenHeight - Theme.BarHeight);
                this.current.Draw(c);
                this.DrawStatusBar(c, now);
            }
            if (this.State.AlertKind != PortalState.AlertNone) AlertOverlay.Draw(c, this.State);
            c.Flush();
            this.dirty = false;
            this.drawnStamp = this.current.Stamp;
        }

        void DrawStatusBar(Canvas c, DateTime now) {
            var s = this.State;
            c.FillRect(Theme.PanelDark, 0, 0, Theme.ScreenWidth, Theme.BarHeight);

            // Left: product name on the desktop, back arrow and title in an app.
            if (this.current == this.Home) {
                Icons.Logo(c, 7, (Theme.BarHeight - Icons.LogoSize) / 2);
                c.DrawText("FEZ Portal Desktop", UiFont.Large, Theme.White, 35, 5);
            }
            else {
                Icons.Back(c, 10, 15, Theme.Accent);
                c.DrawText(c.Ellipsize(this.current.Title, UiFont.Large, 160), UiFont.Large, Theme.White, 28, 5);
            }

            // Right: SD card, Wi-Fi, then time (in apps) and any screen text.
            var right = Theme.ScreenWidth - 10;
            Icons.SdCard(c, right - 12, 8, s.SdPresent ? Theme.White : Theme.TextDim, Theme.PanelDark);
            if (!s.SdPresent) c.ThickLine(Theme.Down, right - 14, 24, right + 1, 7, 2);
            right -= 22;

            var bars = 0;
            if (s.Wifi == WifiStatus.Connected) bars = s.SignalStrength == 0 || s.SignalStrength > -60 ? 3 : s.SignalStrength > -72 ? 2 : 1;
            Icons.Wifi(c, right - 20, 7, bars, Theme.White, s.Wifi == WifiStatus.Failed ? Theme.Down : Theme.TextDim);
            right -= 28;

            string text;
            if (this.current == this.Home) {
                text = s.Wifi == WifiStatus.Connected ? s.WifiSsid : s.WifiMessage;
                c.DrawTextRight(c.Ellipsize(text, UiFont.Small, 220), UiFont.Small, Theme.TextMuted, right, 8);
            }
            else {
                text = Format.ShortTime(now, s.Config.Use24Hour);
                c.DrawTextRight(text, UiFont.Medium, Theme.White, right, 6);
                right -= c.MeasureText(text, UiFont.Medium) + 12;
                right = this.DrawBarButton(c, right);
                var extra = this.current.BarText;
                if (extra != null) {
                    // Room left after the title, minus the dot.
                    var dot = this.current.HasBarDot ? 12 : 0;
                    var left = 28 + c.MeasureText(c.Ellipsize(this.current.Title, UiFont.Large, 160), UiFont.Large) + 14;
                    extra = c.Ellipsize(extra, UiFont.Small, right - left - dot);
                    c.DrawTextRight(extra, UiFont.Small, Theme.TextMuted, right, 8);
                    if (dot > 0) c.FillCircle(this.current.BarDotColor, right - c.MeasureText(extra, UiFont.Small) - 8, 15, 3);
                }
            }
            if (this.current == this.Home) this.barButtonLeft = this.barButtonRight = 0;
            this.drawnStatus = s.StatusVersion;
            this.drawnBarText = this.current.BarText;
        }

        /// <summary>The screen's status bar button, right-aligned at <paramref name="right"/>; returns the room left of it.</summary>
        int DrawBarButton(Canvas c, int right) {
            var label = this.current.BarButton;
            this.barButtonLeft = this.barButtonRight = 0;
            if (label == null) return right;
            const int y = 4, h = Theme.BarHeight - 8;
            var plus = label == "+";
            var w = plus ? 26 : c.MeasureText(label, UiFont.Small) + 18;
            var x = right - w;
            if (plus) {
                c.FillRoundRect(Theme.Accent, x, y, w, h, 7);
                var cx = x + w / 2;
                var cy = y + h / 2;
                c.FillRect(Theme.PanelDark, cx - 6, cy - 1, 13, 3);
                c.FillRect(Theme.PanelDark, cx - 1, cy - 6, 3, 13);
            }
            else {
                c.FillRoundRect(Theme.PanelLight, x, y, w, h, 7);
                c.DrawTextCentered(label, UiFont.Small, Theme.White, x + w / 2, y + (h - c.LineHeight(UiFont.Small)) / 2);
            }
            this.barButtonLeft = x;
            this.barButtonRight = right;
            return x - 10;
        }
    }
}
