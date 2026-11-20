using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Alarm: set the time with the arrows (hour, tens of minutes, minutes), switch it on, and
    /// pick how often it repeats. It rings on any screen, with Snooze and Stop.
    /// </summary>
    public class AlarmScreen : Screen {
        const int CardX = Theme.Margin, CardY = 38, CardW = 300, CardH = 168;
        const int UpY = CardY + 20, DigitsY = CardY + 32, DownY = CardY + 114, InfoY = CardY + 136;
        const int RightX = 316, RightW = 156, SwitchY = 38, RepeatY = 126, RightH = 80;
        const int BottomY = 214, BottomH = 50, TestW = 200;
        const int ArrowW = 38, ArrowGap = 6;   // narrow arrows: the minute digits are close together

        public AlarmScreen(Shell shell) : base(shell) { }

        public override string Title => "Alarm";

        /// <summary>Where back leads: the Clock page, or the desktop when opened from its bell.</summary>
        public Screen ReturnTo;

        public override Screen BackTo => this.ReturnTo ?? this.Shell.ClockApp;

        // The next-ring line changes with the minute.
        public override int Stamp => Clock.Now.Minute;

        AlarmClock Alarm => this.State.Config.Alarm;

        // -- Drawing -------------------------------------------------------------------

        public override void Draw(Canvas c) {
            var a = this.Alarm;
            var now = Clock.Now;
            var use24 = this.State.Config.Use24Hour;

            // Time with arrows over the hour, the tens of minutes and the minutes.
            Card(c, CardX, CardY, CardW, CardH);
            string time, ampm;
            this.TimeText(out time, out ampm);
            int x0, colonX, hourCx, tensCx, onesCx;
            this.Layout(c, time, ampm, out x0, out colonX, out hourCx, out tensCx, out onesCx);
            c.DrawText(time, UiFont.Clock, a.Enabled ? Theme.White : Theme.TextMuted, x0, DigitsY);
            if (!use24) c.DrawText(ampm, UiFont.Large, Theme.Accent, x0 + c.MeasureText(time, UiFont.Clock) + 6, DigitsY + 34);
            foreach (var cx in new[] { hourCx, tensCx, onesCx }) {
                Widgets.Arrow(c, cx, UpY, true, ArrowW);
                Widgets.Arrow(c, cx, DownY, false, ArrowW);
            }

            string info;
            var next = a.Next(now);
            if (a.SnoozeUntil != DateTime.MinValue) info = "Snoozed until " + Format.ShortTime(a.SnoozeUntil, use24);
            else if (next == DateTime.MinValue) info = "Alarm is off";
            else info = "Rings in " + Market.Duration((int)((next - now).Ticks / TimeSpan.TicksPerMinute) + 1);
            c.DrawTextCentered(info, UiFont.Medium, Theme.TextMuted, CardX + CardW / 2, InfoY);

            // On/off switch.
            Card(c, RightX, SwitchY, RightW, RightH);
            Icons.Bell(c, RightX + 28, SwitchY + RightH / 2, 13, a.Enabled ? Theme.Accent : Theme.TextDim);
            c.DrawText(a.Enabled ? "On" : "Off", UiFont.XLarge, Theme.White, RightX + 52, SwitchY + 28);
            const int sw = 52, sh = 28;
            var sx = RightX + RightW - sw - 12;
            var sy = SwitchY + (RightH - sh) / 2;
            c.FillPill(a.Enabled ? Theme.Accent : Theme.PanelDark, sx, sy, sw, sh);
            c.FillCircle(Theme.White, a.Enabled ? sx + sw - sh / 2 : sx + sh / 2, sy + sh / 2, sh / 2 - 4);

            // Repeat (tap to change).
            Card(c, RightX, RepeatY, RightW, RightH);
            c.DrawText("Repeat", UiFont.Small, Theme.TextMuted, RightX + 14, RepeatY + 14);
            c.DrawText(AlarmClock.RepeatName(a.Repeat), UiFont.XLarge, Theme.White, RightX + 14, RepeatY + 38);
            Icons.Forward(c, RightX + RightW - 22, RepeatY + RightH / 2, Theme.AccentSoft);

            // Sound test, and when it rings next.
            Widgets.Button(c, Theme.Margin, BottomY, TestW, BottomH, "Test sound", false, true);
            const int nextX = Theme.Margin + TestW + 8;
            Card(c, nextX, BottomY, Theme.ScreenWidth - Theme.Margin - nextX, BottomH);
            var nextText = next == DateTime.MinValue ? "Turn the alarm on to use it"
                : "Next: " + Format.ShortDay(next.DayOfWeek) + " " + Format.ShortMonth(next.Month) + " " + next.Day + ", " + Format.ShortTime(next, use24);
            c.DrawText(c.Ellipsize(nextText, UiFont.Small, Theme.ScreenWidth - Theme.Margin - nextX - 24), UiFont.Small, Theme.White, nextX + 12, BottomY + 17);
        }

        void TimeText(out string time, out string ampm) {
            var a = this.Alarm;
            var use24 = this.State.Config.Use24Hour;
            var hour = use24 ? Format.Two(a.Hour) : ((a.Hour + 11) % 12 + 1).ToString();
            time = hour + ":" + Format.Two(a.Minute);
            ampm = a.Hour < 12 ? "AM" : "PM";
        }

        /// <summary>Where the time goes (centred with AM/PM) and the centres of the three arrow columns.</summary>
        void Layout(Canvas c, string time, string ampm, out int x0, out int colonX, out int hourCx, out int tensCx, out int onesCx) {
            var width = c.MeasureText(time, UiFont.Clock) + (this.State.Config.Use24Hour ? 0 : 6 + c.MeasureText(ampm, UiFont.Large));
            x0 = CardX + (CardW - width) / 2;
            var colon = time.IndexOf(':');
            colonX = x0 + c.MeasureText(time.Substring(0, colon + 1), UiFont.Clock);
            hourCx = x0 + c.MeasureText(time.Substring(0, colon), UiFont.Clock) / 2;
            var tensW = c.MeasureText(time.Substring(colon + 1, 1), UiFont.Clock);
            tensCx = colonX + tensW / 2;
            onesCx = colonX + tensW + c.MeasureText(time.Substring(colon + 2, 1), UiFont.Clock) / 2;
            // Keep the minute arrows apart (each digit is narrower than an arrow).
            if (onesCx - tensCx < ArrowW + ArrowGap) {
                var mid = (tensCx + onesCx) / 2;
                tensCx = mid - (ArrowW + ArrowGap) / 2;
                onesCx = mid + (ArrowW + ArrowGap) / 2;
            }
            if (tensCx - hourCx < ArrowW + ArrowGap) hourCx = tensCx - ArrowW - ArrowGap;
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            var a = this.Alarm;

            if (Inside(x, y, RightX, SwitchY, RightW, RightH)) {
                a.Enabled = !a.Enabled;
                if (!a.Enabled) a.SnoozeUntil = DateTime.MinValue;
                this.Save();
                return;
            }
            if (Inside(x, y, RightX, RepeatY, RightW, RightH)) {
                a.Repeat = (a.Repeat + 1) % 3;
                this.Save();
                return;
            }
            if (Inside(x, y, Theme.Margin, BottomY, TestW, BottomH)) {
                this.Shell.Host.Chirp();
                return;
            }

            var c = this.Shell.Canvas;
            string time, ampm;
            this.TimeText(out time, out ampm);
            int x0, colonX, hourCx, tensCx, onesCx;
            this.Layout(c, time, ampm, out x0, out colonX, out hourCx, out tensCx, out onesCx);
            var columns = new[] { hourCx, tensCx, onesCx };
            for (var i = 0; i < 3; i++) {
                var dir = Widgets.OnArrow(x, y, columns[i], UpY, ArrowW) ? 1 : Widgets.OnArrow(x, y, columns[i], DownY, ArrowW) ? -1 : 0;
                if (dir == 0) continue;
                if (i == 0) a.Hour = (a.Hour + dir + 24) % 24;
                else if (i == 1) a.Minute = ((a.Minute / 10 + dir + 6) % 6) * 10 + a.Minute % 10;
                else a.Minute = a.Minute / 10 * 10 + (a.Minute % 10 + dir + 10) % 10;
                a.Enabled = true;   // setting a time means you want it
                a.SnoozeUntil = DateTime.MinValue;
                this.Save();
                return;
            }
        }

        void Save() {
            this.Shell.Host.SaveSettings(DefaultFiles.ClockName, this.State.Config.ClockText());
            this.Shell.Invalidate();
        }
    }
}
