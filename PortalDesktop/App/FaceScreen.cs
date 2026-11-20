using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Just the clock, in the style chosen on the Clock page (analog or 7-segment), as large as
    /// the screen allows and without the status bar. Opened by swiping on the desktop; tapping
    /// the clock (or swiping again) goes back.
    /// </summary>
    public class FaceScreen : Screen {
        const int Cx = Theme.ScreenWidth / 2, Cy = Theme.ScreenHeight / 2, R = Theme.ScreenHeight / 2 - 6;
        const int DispX = 8, DispY = 8, DispW = Theme.ScreenWidth - 16, DispH = Theme.ScreenHeight - 16;

        public FaceScreen(Shell shell) : base(shell) { }

        public override string Title => "Clock";

        public override bool FullScreen => true;

        int Face => this.State.Config.ClockFace;

        public override void Draw(Canvas c) {
            var now = Clock.Now;
            if (this.Face == SevenSegment.Analog) AnalogClock.Draw(c, Cx, Cy, R, now);
            else SevenSegment.Draw(c, DispX, DispY, DispW, DispH, now, this.State.Config.Use24Hour, this.Face, this.State.Config.Alarm.IsSet, false);
        }

        public override void Tick(Canvas c, DateTime now, DateTime previous) {
            if (this.Face == SevenSegment.Analog) {
                AnalogClock.Draw(c, Cx, Cy, R, now);
                c.Flush(Cx - R, Cy - R, R * 2 + 1, R * 2 + 1);
                return;
            }
            var newMinute = now.Minute != previous.Minute || now.Hour != previous.Hour;
            SevenSegment.Draw(c, DispX, DispY, DispW, DispH, now, this.State.Config.Use24Hour, this.Face, this.State.Config.Alarm.IsSet, !newMinute);
            c.Flush(DispX, DispY, DispW, DispH);
        }

        public override void Tap(int x, int y) {
            bool onClock;
            if (this.Face == SevenSegment.Analog) {
                var dx = x - Cx;
                var dy = y - Cy;
                onClock = dx * dx + dy * dy <= R * R;
            }
            else {
                onClock = Inside(x, y, DispX, DispY, DispW, DispH);
            }
            if (onClock) this.Shell.Show(this.Shell.Home);
        }

        public override void Swipe(int direction) => this.Shell.Show(this.Shell.Home);
    }
}
