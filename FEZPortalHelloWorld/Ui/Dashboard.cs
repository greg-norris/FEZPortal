namespace FEZPortalHelloWorld.Ui {
    /// <summary>
    /// The 480x272 dashboard screen. Layout is three columns:
    ///   A (x 6-198):   weather, power buttons, stats + donut, back/toggles
    ///   B (x 204-340): login, video player
    ///   C (x 346-474): knobs + balance, vertical sliders, now playing
    /// Values below are placeholders until real data is wired in.
    /// </summary>
    public class Dashboard {
        // Placeholder data.
        public string Temperature = "27°";
        public string WeatherTitle = "Lorem Ipsum";
        public string WeatherSubtitle = "Dolor sit amet";
        public double StatBar1 = 0.70;
        public double StatBar2 = 0.35;
        public double DonutUsed = 0.70;         // yellow part of the donut
        public double Knob1 = 0.80;             // 0..1 around the knob's 270 degree arc
        public double Knob2 = 0.20;
        public double Balance = 0.50;           // 0 = L, 1 = R
        public double Slider1 = 0.55;           // 0 = bottom, 1 = top
        public double Slider2 = 0.25;
        public double Slider3 = 0.85;
        public double VideoPosition = 0.55;
        public double Volume = 0.40;
        public bool Toggle1;
        public bool Toggle2 = true;

        const int Radius = 8;

        public void Draw(Canvas c) {
            c.FillRect(Theme.Background, 0, 0, c.Width, c.Height);

            this.DrawWeather(c, 6, 6, 192, 90);
            this.DrawTurnOffButton(c, 6, 102, 93, 24);
            this.DrawRestartButton(c, 105, 102, 93, 24);
            this.DrawStats(c, 6, 132, 192, 98);
            DrawBackIcon(c, 8, 232);
            DrawToggle(c, 74, 240, 48, 24, this.Toggle1);
            DrawToggle(c, 142, 240, 48, 24, this.Toggle2);

            DrawLogin(c, 204, 6, 136, 154);
            this.DrawVideo(c, 204, 166, 136, 100);

            this.DrawKnobs(c, 346, 6, 128, 80);
            this.DrawSliders(c, 346, 92, 128, 134);
            DrawNowPlaying(c, 346, 232, 128, 34);
        }

        // ---------------------------------------------------------------- column A

        void DrawWeather(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);

            // Sun with rays, partly hidden behind a cloud.
            var sx = x + 34;
            var sy = y + 30;
            for (var a = 0; a < 360; a += 45) {
                c.ThickLine(Theme.Accent,
                    Canvas.PolarX(sx, 18, a), Canvas.PolarY(sy, 18, a),
                    Canvas.PolarX(sx, 24, a), Canvas.PolarY(sy, 24, a), 3);
            }
            c.FillCircle(Theme.Accent, sx, sy, 13);

            c.FillCircle(Theme.White, x + 40, y + 52, 11);
            c.FillCircle(Theme.White, x + 58, y + 45, 15);
            c.FillCircle(Theme.White, x + 75, y + 56, 9);
            c.FillRoundRect(Theme.White, x + 22, y + 50, 64, 24, 12);

            var tx = x + 96;
            c.DrawText(this.Temperature, UiFont.Huge, Theme.White, tx, y + 10);
            c.DrawText(this.WeatherTitle, UiFont.Medium, Theme.White, tx, y + 50);
            c.DrawText(this.WeatherSubtitle, UiFont.Tiny, Theme.TextMuted, tx, y + 67);
        }

        void DrawTurnOffButton(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Accent, x, y, w, h, 6);
            c.DrawText("Turn Off", UiFont.Small, Theme.White, x + 8, y + (h - c.LineHeight(UiFont.Small)) / 2);

            var cx = x + w - 13;
            var cy = y + h / 2;
            c.FillCircle(Theme.White, cx, cy, 9);
            c.FillArc(Theme.Panel, cx, cy, 6, 4, 35, 290);   // power symbol ring
            c.FillRect(Theme.Panel, cx - 1, cy - 7, 3, 7);    // power symbol bar
        }

        void DrawRestartButton(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, 6);
            c.DrawText("Restart", UiFont.Small, Theme.Accent, x + 8, y + (h - c.LineHeight(UiFont.Small)) / 2);

            var cx = x + w - 14;
            var cy = y + h / 2;
            c.FillCircle(Theme.Accent, cx, cy, 10);
            // Circular arrow: ring open at the top right, arrowhead at the top pointing clockwise.
            c.FillArc(Theme.Panel, cx, cy, 6, 5, 110, 250);
            c.FillTriangle(Theme.Panel, cx - 1, cy - 9, cx + 4, cy - 5, cx - 1, cy - 1);
        }

        void DrawStats(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);
            c.DrawText("Lorem Ipsum", UiFont.Medium, Theme.White, x + 10, y + 9);

            DrawTagWithBar(c, x + 10, y + 34, "Dolor", this.StatBar1);
            DrawTagWithBar(c, x + 10, y + 62, "Sit Amet", this.StatBar2);

            // Donut: light outer ring, yellow progress ring, filled centre.
            var cx = x + w - 49;
            var cy = y + h / 2;
            c.FillRing(Theme.AccentSoft, cx, cy, 45, 41);
            c.FillRing(Theme.PanelDark, cx, cy, 40, 34);
            c.FillArc(Theme.Accent, cx, cy, 40, 34, 0, 360 * this.DonutUsed);
            c.FillCircle(Theme.Track, cx, cy, 33);

            var percent = (int)((1 - this.DonutUsed) * 100 + 0.5);
            c.DrawTextCentered(percent.ToString() + "%", UiFont.XLarge, Theme.White, cx, cy - 17);
            c.DrawTextCentered("Available", UiFont.Tiny, Theme.White, cx, cy + 5);
        }

        static void DrawTagWithBar(Canvas c, int x, int y, string label, double value) {
            c.FillRoundRect(Theme.White, x, y, 56, 16, 4);
            c.DrawTextCentered(label, UiFont.Tiny, Theme.Track, x + 28, y + 2);
            DrawProgress(c, x, y + 20, 80, 4, value);
        }

        static void DrawBackIcon(Canvas c, int x, int y) {
            // Curved arrow: head pointing left, shaft curving back down on the right.
            c.FillTriangle(Theme.Panel, x, y + 7, x + 10, y, x + 10, y + 14);
            c.FillRect(Theme.Panel, x + 9, y + 5, 17, 5);
            c.FillArc(Theme.Panel, x + 26, y + 11, 6, 2, 0, 180);
            c.FillRect(Theme.Panel, x + 15, y + 13, 11, 5);

            c.FillRect(Theme.Panel, x, y + 22, 44, 14);
            c.DrawTextCentered("BACK", UiFont.Tiny, Theme.White, x + 22, y + 22);
        }

        static void DrawToggle(Canvas c, int x, int y, int w, int h, bool on) {
            c.FillPill(on ? Theme.Accent : Theme.Panel, x, y, w, h);
            var r = h / 2 - 3;
            c.FillCircle(Theme.White, on ? x + w - h / 2 : x + h / 2, y + h / 2, r);
        }

        // ---------------------------------------------------------------- column B

        static void DrawLogin(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);
            var ix = x + 10;
            var iw = w - 20;

            c.DrawText("Login", UiFont.Large, Theme.White, ix, y + 6);
            c.FillRect(Theme.Track, ix, y + 27, iw, 2);

            c.DrawText("Username", UiFont.Tiny, Theme.White, ix, y + 33);
            DrawField(c, ix, y + 47, iw, "Lorem ipsum");

            c.DrawText("Password", UiFont.Tiny, Theme.White, ix, y + 67);
            DrawField(c, ix, y + 81, iw, "********");

            c.DrawText("Forgot Password?", UiFont.Tiny, Theme.Accent, ix, y + 100);

            c.FillRoundRect(Theme.Accent, x + w - 58, y + 115, 48, 17, 4);
            c.DrawTextCentered("Login", UiFont.Small, Theme.White, x + w - 34, y + 116);

            c.FillRoundRect(Theme.AccentSoft, ix, y + 137, iw, 14, 4);
            c.DrawTextCentered("Create an Account", UiFont.Tiny, Theme.White, x + w / 2, y + 137);
        }

        static void DrawField(Canvas c, int x, int y, int w, string text) {
            c.FillRoundRect(Theme.White, x, y, w, 17, 4);
            c.DrawText(text, UiFont.Small, Theme.Panel, x + 6, y + 1);
        }

        void DrawVideo(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);

            // Placeholder for the video frame.
            var vx = x + 6;
            var vy = y + 6;
            var vw = w - 12;
            var vh = 60;
            c.FillGradientV(System.Drawing.Color.FromArgb(24, 52, 96), System.Drawing.Color.FromArgb(214, 128, 52), vx, vy, vw, vh);
            c.FillRect(Theme.AccentSoft, vx, vy + vh, vw, 8);

            var pcx = vx + vw / 2;
            var pcy = vy + vh / 2;
            c.FillCircle(Theme.White, pcx, pcy, 12);
            c.ThickLine(Theme.Panel, pcx - 2, pcy - 5, pcx + 3, pcy, 3);
            c.ThickLine(Theme.Panel, pcx + 3, pcy, pcx - 2, pcy + 5, 3);

            // Transport controls.
            var cy = y + h - 13;
            c.FillRoundRect(Theme.Accent, vx, cy - 5, 11, 11, 2);
            c.FillRect(Theme.White, vx + 3, cy - 2, 5, 5);
            DrawSlider(c, vx + 17, cy, 60, this.VideoPosition, 4);

            var spx = vx + 86;
            c.FillRect(Theme.White, spx, cy - 2, 3, 5);
            c.FillTriangle(Theme.White, spx + 2, cy, spx + 7, cy - 5, spx + 7, cy + 5);
            c.FillArc(Theme.White, spx + 7, cy, 5, 4, 45, 90);
            DrawSlider(c, spx + 15, cy, vx + vw - spx - 18, this.Volume, 3);
        }

        // ---------------------------------------------------------------- column C

        void DrawKnobs(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);
            DrawKnob(c, x + 32, y + 38, "Lorem", this.Knob1);
            DrawKnob(c, x + 96, y + 38, "Ipsum", this.Knob2);

            // Tick marks and the L/R balance slider.
            for (var tx = x + 18; tx <= x + w - 18; tx += 4) {
                var tall = ((tx - x - 18) / 4) % 4 == 0;
                c.FillRect(Theme.White, tx, y + (tall ? 59 : 61), 1, tall ? 5 : 3);
            }
            c.DrawText("L", UiFont.Tiny, Theme.White, x + 6, y + 64);
            c.DrawTextRight("R", UiFont.Tiny, Theme.White, x + w - 6, y + 64);
            DrawSlider(c, x + 18, y + 71, w - 36, this.Balance, 4);
        }

        static void DrawKnob(Canvas c, int cx, int cy, string label, double value) {
            c.DrawTextCentered(label, UiFont.Tiny, Theme.White, cx, cy - 34);
            c.FillArc(Theme.Track, cx, cy, 20, 17, 225, 270);
            c.FillArc(Theme.Accent, cx, cy, 20, 17, 225, 270 * value);
            c.FillCircle(Theme.White, cx, cy, 15);
            var a = 225 + 270 * value;
            c.FillCircle(Theme.Panel, Canvas.PolarX(cx, 10, a), Canvas.PolarY(cy, 10, a), 2);
        }

        void DrawSliders(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);
            DrawVerticalSlider(c, x + 22, y + 10, h - 38, "Lorem", this.Slider1);
            DrawVerticalSlider(c, x + 62, y + 10, h - 38, "Ipsum", this.Slider2);
            DrawVerticalSlider(c, x + 102, y + 10, h - 38, "Dolor", this.Slider3);
        }

        static void DrawVerticalSlider(Canvas c, int cx, int top, int length, string label, double value) {
            for (var i = 0; i <= length; i += 6) {
                var tall = (i / 6) % 2 == 0;
                c.FillRect(Theme.White, cx + 9, top + i, tall ? 6 : 3, 1);
            }
            c.FillPill(Theme.Track, cx - 3, top, 6, length);
            var ky = top + (int)((1 - value) * length);
            c.FillPill(Theme.Accent, cx - 3, ky, 6, top + length - ky);
            c.FillCircle(Theme.White, cx, ky, 7);
            c.DrawTextCentered(label, UiFont.Tiny, Theme.White, cx + 4, top + length + 10);
        }

        static void DrawNowPlaying(Canvas c, int x, int y, int w, int h) {
            c.FillRoundRect(Theme.Panel, x, y, w, h, Radius);

            // Beamed music note.
            var nx = x + 10;
            var ny = y + 8;
            c.FillRect(Theme.Accent, nx + 4, ny + 2, 2, 15);
            c.FillRect(Theme.Accent, nx + 14, ny, 2, 15);
            c.FillPolygon(Theme.Accent, new[] { nx + 4, nx + 16, nx + 16, nx + 4 }, new[] { ny + 2, ny - 1, ny + 3, ny + 6 });
            c.FillCircle(Theme.Accent, nx + 2, ny + 17, 3);
            c.FillCircle(Theme.Accent, nx + 12, ny + 15, 3);

            c.DrawText("Lorem Ipsum", UiFont.Medium, Theme.White, x + 34, y + 3);
            c.DrawText("Dolor sit amet", UiFont.Tiny, Theme.TextMuted, x + 34, y + 19);
        }

        // ---------------------------------------------------------------- shared widgets

        static void DrawProgress(Canvas c, int x, int y, int w, int h, double value) {
            c.FillPill(Theme.Track, x, y, w, h);
            c.FillPill(Theme.Accent, x, y, (int)(w * value), h);
        }

        /// <summary>Horizontal slider: yellow fill to the left of a white knob, centred on cy.</summary>
        static void DrawSlider(Canvas c, int x, int cy, int w, double value, int knobRadius) {
            var kx = x + (int)(w * value);
            c.FillPill(Theme.Track, x, cy - 1, w, 3);
            c.FillPill(Theme.Accent, x, cy - 1, kx - x, 3);
            c.FillCircle(Theme.White, kx, cy, knobRadius);
        }
    }
}
