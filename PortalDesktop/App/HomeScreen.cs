using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// The desktop: a large clock (tap for the Clock page) with the current weather, and buttons
    /// for Calendar, Weather, Stocks and Calculator.
    /// </summary>
    public class HomeScreen : Screen {
        // Clock card
        const int CardX = Theme.Margin, CardY = 38, CardW = 464, CardH = 110;
        const int DividerX = 300;

        // App buttons
        const int ButtonY = 156, ButtonW = 110, ButtonH = 108, ButtonGap = 8;
        static readonly string[] Labels = { "Calendar", "Weather", "Stocks", "Calculator" };

        public HomeScreen(Shell shell) : base(shell) { }

        public override string Title => "FEZ Portal Desktop";

        public override int Stamp {
            get {
                var s = this.State;
                return s.TimeVersion + s.WeatherVersion * 7 + s.MarketVersion * 13 + s.CalendarVersion * 17 + (int)s.Wifi * 100003;
            }
        }

        public override void Draw(Canvas c) {
            var now = Clock.Now;
            Card(c, CardX, CardY, CardW, CardH);
            this.DrawTime(c, now);
            c.FillRect(Theme.PanelLight, DividerX, CardY + 14, 2, CardH - 28);
            this.DrawWeather(c);

            for (var i = 0; i < Labels.Length; i++) this.DrawButton(c, i, now);
        }

        public override void Tick(Canvas c, DateTime now, DateTime previous) {
            if (now.Minute == previous.Minute && now.Hour == previous.Hour) return;
            // The clock text (the Calendar button's date changes with the day, which redraws everything).
            c.FillRect(Theme.Panel, CardX + 8, CardY + 4, DividerX - CardX - 12, CardH - 8);
            this.DrawTime(c, now);
            c.Flush(CardX, CardY, DividerX - CardX, CardH);
        }

        public override void Tap(int x, int y) {
            if (this.bellX >= 0 && Inside(x, y, this.bellX - BellTap, this.bellY - BellTap, BellTap * 2, BellTap * 2)) {
                this.Shell.AlarmApp.ReturnTo = this;
                this.Shell.Show(this.Shell.AlarmApp);
                return;
            }
            if (Inside(x, y, CardX, CardY, DividerX - CardX, CardH)) { this.Shell.Show(this.Shell.ClockApp); return; }
            if (Inside(x, y, DividerX, CardY, CardX + CardW - DividerX, CardH)) { this.Shell.Show(this.Shell.WeatherApp); return; }
            if (y < ButtonY || y >= ButtonY + ButtonH) return;
            for (var i = 0; i < Labels.Length; i++) {
                if (x >= ButtonX(i) && x < ButtonX(i) + ButtonW) {
                    this.Shell.Show(i == 0 ? this.Shell.CalendarApp : i == 1 ? (Screen)this.Shell.WeatherApp : i == 2 ? (Screen)this.Shell.StocksApp : this.Shell.CalculatorApp);
                    return;
                }
            }
        }

        /// <summary>A swipe either way shows the full-screen analog clock.</summary>
        public override void Swipe(int direction) => this.Shell.Show(this.Shell.FaceApp);

        static int ButtonX(int i) => Theme.Margin + i * (ButtonW + ButtonGap);

        // Bell shown next to the time while an alarm is set (tap it for the Alarm page); -1 = not shown.
        int bellX = -1, bellY;
        const int BellR = 9, BellTap = 20;

        bool AlarmSet => this.State.Config.Alarm.IsSet;

        void DrawTime(Canvas c, DateTime now) {
            var use24 = this.State.Config.Use24Hour;
            var time = Format.Time(now, use24);
            var x = CardX + 16;
            var timeRight = x + c.MeasureText(time, UiFont.Clock);
            c.DrawText(time, UiFont.Clock, Theme.White, x, CardY + 2);
            if (!use24) {
                var ampm = Format.AmPm(now);
                c.DrawText(ampm, UiFont.Large, Theme.Accent, timeRight + 6, CardY + 22);
                // Under the AM/PM.
                this.bellX = timeRight + 6 + c.MeasureText(ampm, UiFont.Large) / 2;
                this.bellY = CardY + 58;
            }
            else {
                this.bellX = timeRight + 18;
                this.bellY = CardY + 38;
            }
            if (this.AlarmSet) Icons.Bell(c, this.bellX, this.bellY, BellR, Theme.Accent);
            else this.bellX = -1;
            c.DrawText(Format.LongDate(now), UiFont.Medium, Theme.TextMuted, x + 2, CardY + 78);
        }

        void DrawWeather(Canvas c) {
            var s = this.State;
            var x = DividerX + 14;
            var w = s.Weather;
            if (w == null) {
                Icons.Weather(c, WeatherKind.PartlyCloudy, true, x + 22, CardY + 40, 18, Theme.Panel);
                c.DrawText("Weather", UiFont.Medium, Theme.White, x + 52, CardY + 26);
                c.DrawText(c.Ellipsize(s.WeatherMessage, UiFont.Tiny, CardX + CardW - x - 10), UiFont.Tiny, Theme.TextMuted, x, CardY + 72);
                return;
            }
            Icons.Weather(c, w.Kind, w.IsDay, x + 22, CardY + 40, 22, Theme.Panel);
            c.DrawText(Format.Degrees(w.Temperature), UiFont.Huge, Theme.White, x + 56, CardY + 20);
            c.DrawText(c.Ellipsize(w.Description, UiFont.Small, 146), UiFont.Small, Theme.White, x, CardY + 72);
            var place = s.Location != null ? s.Location.Place : s.Config.PlaceName;
            c.DrawText(c.Ellipsize(place, UiFont.Tiny, 146), UiFont.Tiny, Theme.TextMuted, x, CardY + 88);
        }

        void DrawButton(Canvas c, int i, DateTime now) {
            var s = this.State;
            var x = ButtonX(i);
            var cx = x + ButtonW / 2;
            var cy = ButtonY + 38;
            Card(c, x, ButtonY, ButtonW, ButtonH);

            string hint;
            switch (i) {
                case 0:
                    Icons.CalendarPage(c, cx, cy, 25, now);
                    var count = CalendarEvents.On(s.Events, now.Year, now.Month, now.Day).Count;
                    hint = count == 0 ? "Nothing today" : count == 1 ? "1 event today" : count + " events today";
                    break;
                case 1:
                    var w = s.Weather;
                    Icons.Weather(c, w != null ? w.Kind : WeatherKind.PartlyCloudy, w == null || w.IsDay, cx, cy, 22, Theme.Panel);
                    hint = w != null ? Format.Degrees(w.Temperature) + "  " + w.Description : "No data yet";
                    break;
                case 2:
                    Icons.StockChart(c, cx, cy, 24);
                    hint = s.Stocks.Length == 0 ? "No symbols" : !s.HasMarketStatus ? s.Stocks.Length + " symbols" : MarketText(s);
                    break;
                default:
                    Icons.Calculator(c, cx, cy, 24);
                    var last = this.Shell.CalculatorApp.LastResult;
                    hint = last.Length > 0 ? "= " + last : "Touch calculator";
                    break;
            }
            c.DrawTextCentered(Labels[i], UiFont.Medium, Theme.White, cx, ButtonY + 68);
            c.DrawTextCentered(c.Ellipsize(hint, UiFont.Tiny, ButtonW - 10), UiFont.Tiny, Theme.TextMuted, cx, ButtonY + 87);
        }

        public static string MarketText(PortalState s) {
            if (s.MarketOpen) return "Market open";
            if (s.MarketSession == "pre-market") return "Pre-market";
            if (s.MarketSession == "post-market") return "After hours";
            return "Market closed";
        }
    }
}
