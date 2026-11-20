using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Countdown: set hours, minutes and seconds with the arrows (or a preset), then Start.
    /// It keeps running on other screens and rings (with a pop-up) when it reaches zero.
    /// </summary>
    public class CountdownScreen : Screen {
        const int CardX = Theme.Margin, CardY = 38, CardW = 300, CardH = 168;
        const int UpY = CardY + 20, DigitsY = CardY + 32, DownY = CardY + 114, InfoY = CardY + 132, BarY = CardY + 152;
        const int ButtonX = 316, ButtonW = 156, StartY = 38, ResetY = 126, ButtonH = 80;
        const int PresetY = 214, PresetH = 50, PresetGap = 5;

        static readonly int[] Presets = { 60, 180, 300, 600, 900, 1800, 3600 };
        static readonly string[] PresetLabels = { "1 min", "3 min", "5 min", "10 min", "15 min", "30 min", "1 hour" };

        long shownSeconds = -1;

        public CountdownScreen(Shell shell) : base(shell) { }

        public override string Title => "Countdown";

        public override Screen BackTo => this.Shell.ClockApp;

        CountdownTimer Timer => this.State.Countdown;

        int Length => this.State.Config.CountdownSeconds;

        long LeftSeconds => (this.Timer.RemainingMs(Clock.UtcNow, this.Length) + 999) / 1000;

        // -- Drawing -------------------------------------------------------------------

        public override void Draw(Canvas c) {
            Card(c, CardX, CardY, CardW, CardH);
            this.DrawDisplay(c);

            var t = this.Timer;
            Widgets.Button(c, ButtonX, StartY, ButtonW, ButtonH, t.Running ? "Pause" : t.Paused ? "Resume" : "Start", true, this.Length > 0);
            Widgets.Button(c, ButtonX, ResetY, ButtonW, ButtonH, "Reset", false, t.Active);

            var w = (Theme.ScreenWidth - Theme.Margin * 2 - PresetGap * (Presets.Length - 1)) / Presets.Length;
            for (var i = 0; i < Presets.Length; i++) {
                var x = Theme.Margin + i * (w + PresetGap);
                var selected = Presets[i] == this.Length;
                c.FillRoundRect(selected ? Theme.AccentSoft : Theme.Panel, x, PresetY, w, PresetH, 10);
                c.DrawTextCentered(PresetLabels[i], UiFont.Small, selected ? Theme.PanelDark : t.Active ? Theme.TextDim : Theme.White,
                    x + w / 2, PresetY + 17);
            }
        }

        /// <summary>Digits, plus arrows when stopped or "Ends at" and a progress bar when running.</summary>
        void DrawDisplay(Canvas c) {
            var t = this.Timer;
            var left = this.LeftSeconds;
            this.shownSeconds = left;
            var text = Format.Hms(left);
            var x0 = CardX + (CardW - c.MeasureText(text, UiFont.Clock)) / 2;
            c.DrawText(text, UiFont.Clock, t.Active && left <= 10 ? Theme.Accent : Theme.White, x0, DigitsY);

            if (!t.Active) {
                for (var i = 0; i < 3; i++) {
                    var cx = PairCenter(c, text, x0, i);
                    Widgets.Arrow(c, cx, UpY, true);
                    Widgets.Arrow(c, cx, DownY, false);
                }
                c.DrawTextCentered("Set the time, then Start", UiFont.Small, Theme.TextMuted, CardX + CardW / 2, InfoY + 4);
                return;
            }

            string info;
            if (t.Running) info = "Ends at " + Format.ShortTime(Clock.ToLocal(t.EndUtc), this.State.Config.Use24Hour);
            else info = "Paused";
            c.DrawTextCentered(info, UiFont.Medium, Theme.TextMuted, CardX + CardW / 2, InfoY);
            var total = this.Length > 0 ? this.Length : 1;
            Widgets.Progress(c, CardX + 20, BarY, CardW - 40, (double)left / total, Theme.Accent);
        }

        /// <summary>Centre x of digit pair i (0 = hours) in the "HH:MM:SS" text drawn at x0.</summary>
        static int PairCenter(Canvas c, string text, int x0, int i) =>
            x0 + c.MeasureText(text.Substring(0, i * 3), UiFont.Clock) + c.MeasureText(text.Substring(i * 3, 2), UiFont.Clock) / 2;

        /// <summary>Redraws the digits whenever the second shown changes.</summary>
        public override void Frame(Canvas c) {
            if (!this.Timer.Running || this.LeftSeconds == this.shownSeconds) return;
            c.FillRect(Theme.Panel, CardX + 6, CardY + 6, CardW - 12, CardH - 12);
            this.DrawDisplay(c);
            c.Flush(CardX, CardY, CardW, CardH);
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            var t = this.Timer;
            var utc = Clock.UtcNow;

            if (Inside(x, y, ButtonX, StartY, ButtonW, ButtonH)) {
                if (t.Running) t.Pause(utc);
                else t.Start(utc, this.Length);
                this.Shell.Invalidate();
                return;
            }
            if (Inside(x, y, ButtonX, ResetY, ButtonW, ButtonH)) {
                t.Reset();
                this.Shell.Invalidate();
                return;
            }
            if (t.Active) return;   // the length can't change mid-run

            if (y >= PresetY && y < PresetY + PresetH) {
                var w = (Theme.ScreenWidth - Theme.Margin * 2 - PresetGap * (Presets.Length - 1)) / Presets.Length;
                var i = (x - Theme.Margin) / (w + PresetGap);
                if (i >= 0 && i < Presets.Length) this.SetLength(Presets[i]);
                return;
            }

            // Arrows: which pair (hours, minutes, seconds), up or down.
            var c = this.Shell.Canvas;
            var text = Format.Hms(this.Length);
            var x0 = CardX + (CardW - c.MeasureText(text, UiFont.Clock)) / 2;
            for (var i = 0; i < 3; i++) {
                var cx = PairCenter(c, text, x0, i);
                var dir = Widgets.OnArrow(x, y, cx, UpY) ? 1 : Widgets.OnArrow(x, y, cx, DownY) ? -1 : 0;
                if (dir == 0) continue;
                int h = this.Length / 3600, m = this.Length / 60 % 60, s = this.Length % 60;
                if (i == 0) h = (h + dir + 24) % 24;
                else if (i == 1) m = (m + dir + 60) % 60;
                else s = (s / 5 * 5 + dir * 5 + 60) % 60;   // seconds go in fives
                this.SetLength(h * 3600 + m * 60 + s);
                return;
            }
        }

        void SetLength(int seconds) {
            this.State.Config.CountdownSeconds = seconds;
            this.Shell.Host.SaveSettings(DefaultFiles.ClockName, this.State.Config.ClockText());
            this.Shell.Invalidate();
        }
    }
}
