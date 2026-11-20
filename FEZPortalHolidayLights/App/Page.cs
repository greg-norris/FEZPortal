using System.Drawing;
using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.App {
    /// <summary>
    /// One full-screen page. Everything runs on the UI thread; settings are changed through
    /// <see cref="Change"/> so the light thread sees whole changes.
    /// </summary>
    public abstract class Page {
        protected readonly LightsApp App;

        protected Page(LightsApp app) {
            this.App = app;
        }

        protected LightSettings Settings => this.App.Settings;

        /// <summary>Called when the page is shown.</summary>
        public virtual void Opened() { }

        /// <summary>Draws the whole page (the app flushes the screen afterwards).</summary>
        public abstract void Draw(Canvas c);

        /// <summary>On every pass of the UI loop: redraw and flush whatever changed.</summary>
        public virtual void Frame(Canvas c) { }

        public virtual void Down(int x, int y) { }
        public virtual void Move(int x, int y) { }
        public virtual void Up(int x, int y) { }

        /// <summary>Marks a settings change (call inside lock(Settings) after changing a field).</summary>
        protected void Changed() => this.Settings.Version++;

        protected static bool Inside(int x, int y, int left, int top, int width, int height) =>
            x >= left && x < left + width && y >= top && y < top + height;

        /// <summary>A rounded button: yellow when selected, blue otherwise.</summary>
        protected static void Button(Canvas c, int x, int y, int w, int h, string label, bool selected, UiFont font) {
            c.FillRoundRect(selected ? Theme.Accent : Theme.Panel, x, y, w, h, Theme.Radius);
            c.DrawTextCentered(label, font, selected ? Theme.PanelDark : Theme.White, x + w / 2, y + (h - c.LineHeight(font)) / 2);
        }

        /// <summary>A small pill-shaped button for the status bar.</summary>
        protected static void BarButton(Canvas c, int x, int w, string label, Color bg, Color fg) {
            c.FillRoundRect(bg, x, 4, w, Theme.BarHeight - 8, 7);
            c.DrawTextCentered(label, UiFont.Small, fg, x + w / 2, 4 + (Theme.BarHeight - 8 - c.LineHeight(UiFont.Small)) / 2);
        }

        // -- Status bar: title (with a back arrow on inner pages), the estimated current, ON / OFF -----

        protected const int PowerX = 406, PowerW = 66, BackW = 150;

        int drawnMilliamps = -1;
        bool drawnLimited;

        protected void DrawBar(Canvas c, string title, bool back) {
            var s = this.Settings;
            c.FillRect(Theme.PanelDark, 0, 0, Theme.ScreenWidth, Theme.BarHeight);
            var x = 10;
            if (back) {
                c.ThickLine(Theme.Accent, 18, 8, 11, 15, 3);
                c.ThickLine(Theme.Accent, 11, 15, 18, 22, 3);
                x = 28;
            }
            c.DrawText(title, UiFont.Large, Theme.White, x, 6);

            // Estimated current, in yellow while the power limit is dimming the strip.
            int milliamps, wanted;
            lock (this.App.Engine) {
                milliamps = this.App.Engine.Milliamps;
                wanted = this.App.Engine.WantedMilliamps;
            }
            var limited = wanted > milliamps + 5;
            x += c.MeasureText(title, UiFont.Large) + 12;
            var text = !s.On ? "Off" : limited ? "Limited to " + Amps(s.PowerLimit) : "About " + Amps(milliamps);
            c.DrawText(text, UiFont.Small, limited && s.On ? Theme.Accent : Theme.TextMuted, x, 9);
            this.drawnMilliamps = milliamps;
            this.drawnLimited = limited;

            if (s.On) BarButton(c, PowerX, PowerW, "ON", Theme.Up, Theme.PanelDark);
            else BarButton(c, PowerX, PowerW, "OFF", Theme.PanelLight, Theme.TextDim);
        }

        /// <summary>True when the current shown in the bar is out of date (it changes with the pattern).</summary>
        protected bool BarOutOfDate() {
            int milliamps, wanted;
            lock (this.App.Engine) {
                milliamps = this.App.Engine.Milliamps;
                wanted = this.App.Engine.WantedMilliamps;
            }
            var limited = wanted > milliamps + 5;
            return (milliamps + 50) / 100 != (this.drawnMilliamps + 50) / 100 || limited != this.drawnLimited;
        }

        /// <summary>Turns the lights on or off when x is on the ON / OFF button; true if it was.</summary>
        protected bool PowerTap(int x) {
            if (x < PowerX - 6) return false;
            var s = this.Settings;
            lock (s) { s.On = !s.On; this.Changed(); }
            return true;
        }

        /// <summary>Picks an effect (and turns the lights on, if they were off).</summary>
        protected void PickEffect(int effect) {
            var s = this.Settings;
            lock (s) {
                s.Effect = effect;
                if (!s.On) s.On = true;
                this.Changed();
            }
        }

        // -- Brightness and speed sliders, at the bottom of the main and holiday pages -------------

        protected const int BrightnessY = 202, SpeedY = 238, SliderH = 34, TrackX = 118, TrackW = 282, ValueRight = 472;

        protected void DrawSliders(Canvas c) {
            var s = this.Settings;
            c.FillRect(Theme.Background, 0, BrightnessY, Theme.ScreenWidth, Theme.ScreenHeight - BrightnessY);
            Slider(c, BrightnessY, "Brightness", s.Brightness, s.Brightness + "%");
            // "0.25x" below normal speed, "2.5x" above it.
            var hundredths = (int)(LightEngine.Rate(s.Speed) * 100 + 0.5);
            var tenths = (hundredths + 5) / 10;
            var rate = hundredths < 100 ? "0." + (hundredths < 10 ? "0" : "") + hundredths : tenths / 10 + "." + tenths % 10;
            Slider(c, SpeedY, "Speed", s.Speed, rate + "x");
        }

        protected static void FlushSliders(Canvas c) => c.Flush(0, BrightnessY, Theme.ScreenWidth, Theme.ScreenHeight - BrightnessY);

        static void Slider(Canvas c, int y, string label, int value, string valueText) {
            var cy = y + SliderH / 2;
            c.DrawText(label, UiFont.Medium, Theme.TextMuted, 10, cy - 8);
            c.FillRoundRect(Theme.Track, TrackX, cy - 3, TrackW, 6, 3);
            var kx = TrackX + value * TrackW / 100;
            if (kx > TrackX) c.FillRoundRect(Theme.Accent, TrackX, cy - 3, kx - TrackX, 6, 3);
            c.FillCircle(Theme.White, kx, cy, 10);
            c.DrawTextRight(valueText, UiFont.Large, Theme.White, ValueRight, cy - 9);
        }

        /// <summary>Sets brightness (or speed) from a finger at x; true when the value changed.</summary>
        protected bool SetSlider(bool brightness, int x) {
            var value = LightSettings.Clamp(((x - TrackX) * 100 + TrackW / 2) / TrackW, 0, 100);
            var s = this.Settings;
            lock (s) {
                if (brightness) {
                    if (s.Brightness == value) return false;
                    s.Brightness = value;
                }
                else {
                    if (s.Speed == value) return false;
                    s.Speed = value;
                }
                this.Changed();
            }
            return true;
        }

        /// <summary>A row of colour dots (a theme's or holiday's colours), centred on cx.</summary>
        protected static void Dots(Canvas c, int[] colors, int count, int cx, int cy) {
            const int r = 5, pitch = 15;
            var x = cx - (count - 1) * pitch / 2;
            for (var i = 0; i < count; i++) {
                var rgb = colors[i % colors.Length];
                c.FillCircle(Theme.PanelDark, x, cy, r + 1);   // an outline, so dark colours show on blue and yellow
                c.FillCircle(Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF), x, cy, r);
                x += pitch;
            }
        }

        /// <summary>"0.42 A" from milliamps.</summary>
        protected static string Amps(int milliamps) {
            var hundredths = (milliamps + 5) / 10;
            var frac = hundredths % 100;
            return hundredths / 100 + "." + (frac < 10 ? "0" : "") + frac + " A";
        }
    }
}
