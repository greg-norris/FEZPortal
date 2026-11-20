using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Timer: a stopwatch with tenths of a second and laps. It keeps running on other screens.
    /// The fastest lap is shown in green and the slowest in red once there are three or more.
    /// </summary>
    public class TimerScreen : Screen {
        const int CardX = Theme.Margin, CardY = 38, CardW = 300, CardH = 120;
        const int DigitsY = CardY + 18, LapLineY = CardY + 92;
        const int ButtonY = 166, ButtonH = 98, ButtonW = 146;
        const int StartX = Theme.Margin, LapX = Theme.Margin + ButtonW + 8;
        const int LapsX = 316, LapsY = 38, LapsW = 156, LapsH = 226, LapRowsY = LapsY + 30, LapPitch = 21, LapRows = 9;

        long shownTenths = -1;

        public TimerScreen(Shell shell) : base(shell) { }

        public override string Title => "Timer";

        public override Screen BackTo => this.Shell.ClockApp;

        LapTimer Watch => this.State.Stopwatch;

        // -- Drawing -------------------------------------------------------------------

        public override void Draw(Canvas c) {
            var w = this.Watch;
            Card(c, CardX, CardY, CardW, CardH);
            this.DrawDisplay(c);

            var elapsed = w.ElapsedMs(Clock.UtcNow);
            Widgets.Button(c, StartX, ButtonY, ButtonW, ButtonH, w.Running ? "Stop" : elapsed > 0 ? "Resume" : "Start", true, true);
            if (w.Running || elapsed == 0) Widgets.Button(c, LapX, ButtonY, ButtonW, ButtonH, "Lap", false, w.Running);
            else Widgets.Button(c, LapX, ButtonY, ButtonW, ButtonH, "Reset", false, true);

            this.DrawLaps(c);
        }

        void DrawDisplay(Canvas c) {
            var w = this.Watch;
            var ms = w.ElapsedMs(Clock.UtcNow);
            this.shownTenths = ms / 100;
            c.DrawTextCentered(Format.Stopwatch(ms), UiFont.Clock, Theme.White, CardX + CardW / 2, DigitsY);

            // The lap in progress, under the total.
            string line;
            if (w.LapCount > 0) line = "Lap " + (w.LapCount + 1) + "   " + Format.Stopwatch(ms - w.Laps[w.LapCount - 1]);
            else line = w.Running ? "Tap Lap to split" : ms > 0 ? "Stopped" : "Tap Start";
            c.DrawTextCentered(line, UiFont.Medium, Theme.TextMuted, CardX + CardW / 2, LapLineY);
        }

        void DrawLaps(Canvas c) {
            var w = this.Watch;
            Card(c, LapsX, LapsY, LapsW, LapsH);
            c.DrawText("Laps", UiFont.Medium, Theme.White, LapsX + 12, LapsY + 8);
            if (w.LapCount == 0) {
                c.DrawText("None yet", UiFont.Small, Theme.TextDim, LapsX + 12, LapRowsY + 4);
                return;
            }

            // Fastest and slowest, when there are enough laps to compare.
            int fast = -1, slow = -1;
            if (w.LapCount >= 3) {
                fast = slow = 0;
                for (var i = 1; i < w.LapCount; i++) {
                    if (w.LapLength(i) < w.LapLength(fast)) fast = i;
                    if (w.LapLength(i) > w.LapLength(slow)) slow = i;
                }
            }

            // Newest first.
            for (var row = 0; row < LapRows && row < w.LapCount; row++) {
                var i = w.LapCount - 1 - row;
                var y = LapRowsY + row * LapPitch;
                var color = i == fast ? Theme.Up : i == slow ? Theme.Down : Theme.White;
                c.DrawText("Lap " + (i + 1), UiFont.Tiny, Theme.TextMuted, LapsX + 12, y + 2);
                c.DrawTextRight(Format.Stopwatch(w.LapLength(i)), UiFont.Small, color, LapsX + LapsW - 12, y);
            }
        }

        /// <summary>Redraws the digits whenever the tenth shown changes.</summary>
        public override void Frame(Canvas c) {
            if (!this.Watch.Running || this.Watch.ElapsedMs(Clock.UtcNow) / 100 == this.shownTenths) return;
            c.FillRect(Theme.Panel, CardX + 6, CardY + 6, CardW - 12, CardH - 12);
            this.DrawDisplay(c);
            c.Flush(CardX, CardY, CardW, CardH);
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            var w = this.Watch;
            var utc = Clock.UtcNow;
            if (Inside(x, y, StartX, ButtonY, ButtonW, ButtonH)) {
                if (w.Running) w.Stop(utc);
                else w.Start(utc);
            }
            else if (Inside(x, y, LapX, ButtonY, ButtonW, ButtonH)) {
                if (w.Running) w.Lap(utc);
                else w.Reset();
            }
            else {
                return;
            }
            this.Shell.Invalidate();
        }
    }
}
