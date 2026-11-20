using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>Current conditions, today's details and a 5-day forecast.</summary>
    public class WeatherScreen : Screen {
        const int NowX = Theme.Margin, NowY = 38, NowW = 300, NowH = 140;
        const int InfoX = 316, InfoY = 38, InfoW = 156, InfoH = 140;
        const int DayY = 186, DayW = 88, DayH = 78, DayGap = 6;

        public WeatherScreen(Shell shell) : base(shell) { }

        public override string Title => "Weather";

        public override int Stamp => this.State.WeatherVersion;

        public override string BarText {
            get {
                var w = this.State.Weather;
                return w == null ? null : "Updated " + Format.ShortTime(w.Updated, this.State.Config.Use24Hour);
            }
        }

        public override void Draw(Canvas c) {
            var s = this.State;
            var w = s.Weather;
            Card(c, NowX, NowY, NowW, NowH);
            Card(c, InfoX, InfoY, InfoW, InfoH);
            var place = s.Location != null ? s.Location.Place : s.Config.PlaceName;
            c.DrawText(c.Ellipsize(place.Length > 0 ? place : "Your area", UiFont.Small, NowW - 24), UiFont.Small, Theme.TextMuted, NowX + 12, NowY + 10);

            if (w == null) {
                Icons.Weather(c, WeatherKind.PartlyCloudy, true, NowX + 62, NowY + 80, 36, Theme.Panel);
                c.DrawText("No weather yet", UiFont.Large, Theme.White, NowX + 120, NowY + 56);
                c.DrawText(c.Ellipsize(s.WeatherMessage, UiFont.Small, NowW - 132), UiFont.Small, Theme.TextMuted, NowX + 120, NowY + 82);
                for (var i = 0; i < WeatherData.Days; i++) Card(c, DayX(i), DayY, DayW, DayH);
                return;
            }

            var units = s.Config.Metric ? " km/h" : " mph";
            Icons.Weather(c, w.Kind, w.IsDay, NowX + 62, NowY + 78, 38, Theme.Panel);
            c.DrawText(Format.Degrees(w.Temperature), UiFont.Clock, Theme.White, NowX + 118, NowY + 18);
            c.DrawText(c.Ellipsize(w.Description, UiFont.Large, NowW - 132), UiFont.Large, Theme.White, NowX + 122, NowY + 88);
            c.DrawText("Feels like " + Format.Degrees(w.FeelsLike), UiFont.Small, Theme.TextMuted, NowX + 122, NowY + 112);

            var y = InfoY + 10;
            this.InfoRow(c, ref y, "Humidity", w.Humidity + "%");
            this.InfoRow(c, ref y, "Wind", Format.Round(w.Wind) + units);
            if (w.DayHigh.Length > 0) this.InfoRow(c, ref y, "High / Low", Format.Degrees(w.DayHigh[0]) + " / " + Format.Degrees(w.DayLow[0]));
            if (w.Sunrise != DateTime.MinValue) this.InfoRow(c, ref y, "Sunrise", Format.ShortTime(w.Sunrise, s.Config.Use24Hour));
            if (w.Sunset != DateTime.MinValue) this.InfoRow(c, ref y, "Sunset", Format.ShortTime(w.Sunset, s.Config.Use24Hour));

            for (var i = 0; i < WeatherData.Days; i++) {
                var x = DayX(i);
                Card(c, x, DayY, DayW, DayH);
                if (i >= w.DayDates.Length) continue;
                var cx = x + DayW / 2;
                var label = i == 0 ? "Today" : Format.ShortDay(w.DayDates[i].DayOfWeek);
                c.DrawTextCentered(label, UiFont.Small, Theme.White, cx, DayY + 6);
                Icons.Weather(c, WeatherData.KindOf(w.DayCodes[i]), true, cx, DayY + 39, 15, Theme.Panel);
                var high = Format.Degrees(w.DayHigh[i]);
                var low = Format.Degrees(w.DayLow[i]);
                var total = c.MeasureText(high, UiFont.Small) + 8 + c.MeasureText(low, UiFont.Small);
                var tx = cx - total / 2;
                c.DrawText(high, UiFont.Small, Theme.White, tx, DayY + 58);
                c.DrawText(low, UiFont.Small, Theme.TextDim, tx + total - c.MeasureText(low, UiFont.Small), DayY + 58);
            }
        }

        public override void Tap(int x, int y) {
            if (!Inside(x, y, NowX, NowY, NowW, NowH)) return;
            this.State.WeatherMessage = "Refreshing...";
            this.State.WeatherVersion++;
            this.Shell.Host.RefreshWeather();
        }

        void InfoRow(Canvas c, ref int y, string label, string value) {
            c.DrawText(label, UiFont.Tiny, Theme.TextDim, InfoX + 12, y + 1);
            c.DrawTextRight(value, UiFont.Small, Theme.White, InfoX + InfoW - 12, y);
            y += 25;
        }

        static int DayX(int i) => Theme.Margin + i * (DayW + DayGap);
    }
}
