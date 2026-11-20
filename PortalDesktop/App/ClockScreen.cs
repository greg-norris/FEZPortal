using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Analog or 7-segment clock (tap it to change: analog, red, green, blue), digital time and
    /// date, buttons for the Countdown, Timer and Alarm pages (each showing its state), and a
    /// 12/24 hour switch.
    /// </summary>
    public class ClockScreen : Screen {
        const int FaceX = 130, FaceY = 152, FaceR = 108;
        // The 7-segment display uses the whole space left of the panel.
        const int DispX = Theme.Margin, DispY = 38, DispW = 236, DispH = 226;
        const int PanelX = 252, PanelY = 38, PanelW = 220, PanelH = 226;
        const int TextX = PanelX + 16;
        const int ToggleY = PanelY + PanelH - 38, ToggleH = 26, ToggleW = 92;
        const int ToolY = PanelY + 108, ToolH = 72, ToolW = 60, ToolGap = 4;
        static readonly string[] ToolNames = { "Countdown", "Timer", "Alarm" };

        public ClockScreen(Shell shell) : base(shell) { }

        public override string Title => "Clock";

        public override int Stamp => this.State.TimeVersion + (int)this.State.Wifi * 100003;

        public override void Draw(Canvas c) {
            var now = Clock.Now;
            this.DrawFace(c, now);

            var s = this.State;
            Card(c, PanelX, PanelY, PanelW, PanelH);
            this.DrawDigital(c, now);
            c.DrawText(Format.DayName(now.DayOfWeek), UiFont.Medium, Theme.TextMuted, TextX, PanelY + 54);
            c.DrawText(Format.MonthName(now.Month) + " " + now.Day + ", " + now.Year, UiFont.Large, Theme.White, TextX, PanelY + 72);
            c.FillRect(Theme.PanelLight, TextX, PanelY + 100, PanelW - 32, 1);

            this.DrawTools(c);

            this.DrawToggle(c, TextX, "12 hour", !s.Config.Use24Hour);
            this.DrawToggle(c, TextX + ToggleW + 4, "24 hour", s.Config.Use24Hour);
        }

        public override void Tick(Canvas c, DateTime now, DateTime previous) {
            if (this.Face == SevenSegment.Analog) {
                this.DrawFace(c, now);
                c.Flush(FaceX - FaceR, FaceY - FaceR, FaceR * 2 + 1, FaceR * 2 + 1);
            }
            else {
                var newMinute = now.Minute != previous.Minute || now.Hour != previous.Hour;
                SevenSegment.Draw(c, DispX, DispY, DispW, DispH, now, this.State.Config.Use24Hour, this.Face, this.State.Config.Alarm.IsSet, !newMinute);
                c.Flush(DispX, DispY, DispW, DispH);
            }
            c.FillRect(Theme.Panel, TextX - 4, PanelY + 8, PanelW - 24, 40);
            this.DrawDigital(c, now);
            c.Flush(TextX - 4, PanelY + 8, PanelW - 24, 40);
            // The buttons show a running countdown and the stopwatch.
            this.DrawTools(c);
            c.Flush(TextX, ToolY, ToolW * 3 + ToolGap * 2, ToolH);
        }

        static int ToolX(int i) => TextX + i * (ToolW + ToolGap);

        void DrawTools(Canvas c) {
            for (var i = 0; i < ToolNames.Length; i++) {
                var x = ToolX(i);
                var cx = x + ToolW / 2;
                c.FillRoundRect(Theme.PanelLight, x, ToolY, ToolW, ToolH, 10);
                if (i == 0) Icons.Hourglass(c, cx, ToolY + 20, 12);
                else if (i == 1) Icons.Stopwatch(c, cx, ToolY + 20, 12);
                else Icons.Bell(c, cx, ToolY + 20, 12, this.State.Config.Alarm.Enabled ? Theme.Accent : Theme.White);
                c.DrawTextCentered(ToolNames[i], UiFont.Tiny, Theme.White, cx, ToolY + 38);
                var status = this.ToolStatus(i);
                c.DrawTextCentered(c.Ellipsize(status, UiFont.Pill, ToolW - 4), UiFont.Pill,
                    this.ToolActive(i) ? Theme.Accent : Theme.TextMuted, cx, ToolY + 54);
            }
        }

        /// <summary>A line under each button: time left, stopwatch time, alarm time.</summary>
        string ToolStatus(int i) {
            var s = this.State;
            var utc = Clock.UtcNow;
            if (i == 0) {
                var left = (s.Countdown.RemainingMs(utc, s.Config.CountdownSeconds) + 999) / 1000;
                return Format.Span(left);
            }
            if (i == 1) {
                var ms = s.Stopwatch.ElapsedMs(utc);
                return ms == 0 ? "0:00" : Format.Span(ms / 1000);
            }
            var a = s.Config.Alarm;
            if (!a.Enabled) return "Off";
            return Format.Time(new DateTime(2000, 1, 1, a.Hour, a.Minute, 0), s.Config.Use24Hour) + (s.Config.Use24Hour ? "" : a.Hour < 12 ? "a" : "p");
        }

        bool ToolActive(int i) => i == 0 ? this.State.Countdown.Running : i == 1 ? this.State.Stopwatch.Running : this.State.Config.Alarm.Enabled;

        public override void Tap(int x, int y) {
            // Tapping the clock switches its style: analog, red, green, blue (saved in clock.txt).
            if (this.OnFace(x, y)) {
                var config = this.State.Config;
                config.ClockFace = (config.ClockFace + 1) % SevenSegment.FaceCount;
                this.Shell.Host.SaveSettings(DefaultFiles.ClockName, config.ClockText());
                this.Shell.Invalidate();
                return;
            }
            if (y >= ToolY && y < ToolY + ToolH) {
                for (var i = 0; i < ToolNames.Length; i++) {
                    if (x < ToolX(i) || x >= ToolX(i) + ToolW) continue;
                    this.Shell.AlarmApp.ReturnTo = null;
                    this.Shell.Show(i == 0 ? (Screen)this.Shell.CountdownApp : i == 1 ? (Screen)this.Shell.TimerApp : this.Shell.AlarmApp);
                    return;
                }
                return;
            }
            if (y < ToggleY || y >= ToggleY + ToggleH) return;
            bool use24;
            if (x >= TextX && x < TextX + ToggleW) use24 = false;
            else if (x >= TextX + ToggleW + 4 && x < TextX + ToggleW * 2 + 4) use24 = true;
            else return;
            if (use24 == this.State.Config.Use24Hour) return;
            this.Shell.Host.SaveSettings(DefaultFiles.ClockName, this.State.Config.SetUse24Hour(use24));
            this.Shell.Invalidate();
        }

        void DrawDigital(Canvas c, DateTime now) {
            var use24 = this.State.Config.Use24Hour;
            var text = Format.TimeWithSeconds(now, use24);
            c.DrawText(text, UiFont.Huge, Theme.White, TextX, PanelY + 12);
            if (!use24) c.DrawText(Format.AmPm(now), UiFont.Large, Theme.Accent, TextX + c.MeasureText(text, UiFont.Huge) + 6, PanelY + 16);
        }

        void DrawToggle(Canvas c, int x, string label, bool selected) {
            c.FillPill(selected ? Theme.Accent : Theme.PanelDark, x, ToggleY, ToggleW, ToggleH);
            c.DrawTextCentered(label, UiFont.Small, selected ? Theme.PanelDark : Theme.TextMuted, x + ToggleW / 2, ToggleY + 6);
        }

        int Face => this.State.Config.ClockFace;

        void DrawFace(Canvas c, DateTime now) {
            if (this.Face == SevenSegment.Analog) AnalogClock.Draw(c, FaceX, FaceY, FaceR, now);
            else SevenSegment.Draw(c, DispX, DispY, DispW, DispH, now, this.State.Config.Use24Hour, this.Face, this.State.Config.Alarm.IsSet, false);
        }

        /// <summary>Whether a tap is on the clock (the round face, or the digital display).</summary>
        bool OnFace(int x, int y) {
            if (this.Face != SevenSegment.Analog) return Inside(x, y, DispX, DispY, DispW, DispH);
            var dx = x - FaceX;
            var dy = y - FaceY;
            return dx * dx + dy * dy <= FaceR * FaceR;
        }
    }
}
