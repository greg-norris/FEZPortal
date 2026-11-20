using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.App {
    /// <summary>
    /// Strip setup: the colour order and the LED count (checked with a test pattern shown while this
    /// page is open: red, green and blue on the first 30 LEDs, white on the last 10) and the power limit.
    /// </summary>
    public class SetupPage : Page {
        const int CardX = 8, CardW = Theme.ScreenWidth - 16, Label = CardX + 10;
        const int OrderY = 54, OrderH = 96, OrderButtonsY = 112, OrderButtonW = 70, OrderButtonH = 32, OrderGap = 5;

        // LEDs and power limit: a row each, with step buttons in shared columns.
        const int StripY = 156, StripH = 112;
        const int CountY = 162, LimitY = 198, StepH = 32, StepW = 38;
        const int Minus32X = 228, MinusX = 268, ValueX = 350, PlusX = 392, Plus32X = 432;
        const int InfoY = 236, NowY = 251;

        int drawnMilliamps = -1;

        public SetupPage(LightsApp app) : base(app) { }

        public override void Opened() {
            lock (this.Settings) this.App.Engine.TestPattern = true;
        }

        void Close() {
            lock (this.Settings) this.App.Engine.TestPattern = false;
            this.App.Show(this.App.Main);
        }

        // -- Drawing -----------------------------------------------------------------

        public override void Draw(Canvas c) {
            var s = this.Settings;
            c.FillRect(Theme.PanelDark, 0, 0, Theme.ScreenWidth, Theme.BarHeight);
            // Back arrow and title.
            c.ThickLine(Theme.Accent, 18, 8, 11, 15, 3);
            c.ThickLine(Theme.Accent, 11, 15, 18, 22, 3);
            c.DrawText("Setup", UiFont.Large, Theme.White, 28, 6);
            c.DrawTextRight("Strip on SPI2 (PI1 clock, PI3 data)", UiFont.Small, Theme.TextMuted, Theme.ScreenWidth - 10, 9);

            // Colour order.
            c.FillRoundRect(Theme.PanelDark, CardX, OrderY, CardW, OrderH, Theme.Radius);
            c.DrawText("Colour order", UiFont.Large, Theme.White, Label, OrderY + 6);
            c.DrawText("First 10 LEDs red, then 10 green, then 10 blue; the last 10 white.", UiFont.Small, Theme.TextMuted, Label, OrderY + 28);
            c.DrawText("If the colours are in another order, tap the order that matches.", UiFont.Small, Theme.TextMuted, Label, OrderY + 42);
            for (var i = 0; i < LightSettings.Orders.Length; i++) {
                Button(c, OrderX(i), OrderButtonsY, OrderButtonW, OrderButtonH, LightSettings.Orders[i], i == s.Order, UiFont.Large);
            }

            // LED count and power limit.
            c.FillRoundRect(Theme.PanelDark, CardX, StripY, CardW, StripH, Theme.Radius);
            c.DrawText("LEDs", UiFont.Large, Theme.White, Label, CountY + 7);
            c.DrawText("(32 = 1 m)", UiFont.Small, Theme.TextDim, Label + c.MeasureText("LEDs", UiFont.Large) + 8, CountY + 10);
            Step(c, Minus32X, CountY, "-32");
            Step(c, MinusX, CountY, "-1");
            c.DrawTextCentered(s.Count.ToString(), UiFont.XLarge, Theme.Accent, ValueX, CountY + 6);
            Step(c, PlusX, CountY, "+1");
            Step(c, Plus32X, CountY, "+32");

            c.DrawText("Power limit", UiFont.Large, Theme.White, Label, LimitY + 7);
            Step(c, MinusX, LimitY, "-");
            c.DrawTextCentered(Amps(s.PowerLimit), UiFont.XLarge, Theme.Accent, ValueX, LimitY + 6);
            Step(c, PlusX, LimitY, "+");

            c.DrawText("Up to 60 mA per LED at full white: " + s.Count + " LEDs can need " +
                Amps(s.Count * 3 * StripOutput.ChannelMilliamps) + ".", UiFont.Small, Theme.TextMuted, Label, InfoY);
            this.DrawNow(c);
        }

        static void Step(Canvas c, int x, int y, string label) => Button(c, x, y, StepW, StepH, label, false, UiFont.Medium);

        static int OrderX(int i) => CardX + 10 + i * (OrderButtonW + OrderGap) + 3;

        /// <summary>The last line: no SD card warning, or what the test pattern draws.</summary>
        void DrawNow(Canvas c) {
            int milliamps;
            lock (this.App.Engine) milliamps = this.App.Engine.Milliamps;
            c.FillRect(Theme.PanelDark, Label, NowY, CardW - 20, 14);
            if (this.App.Store.Present) {
                c.DrawText("The test pattern draws about " + Amps(milliamps) + ". Settings are saved on the SD card.",
                    UiFont.Small, Theme.TextDim, Label, NowY);
            }
            else {
                c.DrawText("No SD card: changes are forgotten at power-off.", UiFont.Small, Theme.Accent, Label, NowY);
            }
            this.drawnMilliamps = milliamps;
        }

        public override void Frame(Canvas c) {
            int milliamps;
            lock (this.App.Engine) milliamps = this.App.Engine.Milliamps;
            if ((milliamps + 5) / 10 == (this.drawnMilliamps + 5) / 10) return;
            this.DrawNow(c);
            c.Flush(0, NowY, Theme.ScreenWidth, 14);
        }

        // -- Touch -------------------------------------------------------------------

        public override void Down(int x, int y) {
            var s = this.Settings;
            if (y < Theme.BarHeight) {
                if (x < 120) this.Close();
                return;
            }
            if (y >= OrderButtonsY - 4 && y < OrderButtonsY + OrderButtonH + 4) {
                for (var i = 0; i < LightSettings.Orders.Length; i++) {
                    if (x >= OrderX(i) - 2 && x < OrderX(i) + OrderButtonW + 3) {
                        lock (s) { s.Order = i; this.Changed(); }
                        this.App.Invalidate();
                    }
                }
                return;
            }
            if (y >= CountY - 2 && y < CountY + StepH + 2) {
                var step = OnStep(x, Minus32X) ? -32 : OnStep(x, MinusX) ? -1 : OnStep(x, PlusX) ? 1 : OnStep(x, Plus32X) ? 32 : 0;
                if (step == 0) return;
                lock (s) {
                    var count = LightSettings.Clamp(s.Count + step, 1, LightSettings.MaxCount);
                    if (count == s.Count) return;
                    s.Count = count;
                    this.Changed();
                }
                this.App.Invalidate();
                return;
            }
            if (y >= LimitY - 2 && y < LimitY + StepH + 2) {
                var step = OnStep(x, MinusX) ? -1 : OnStep(x, PlusX) ? 1 : 0;
                if (step == 0) return;
                lock (s) {
                    var limit = LightSettings.Clamp(s.PowerLimit + step * LightSettings.PowerLimitStep, LightSettings.MinPowerLimit, LightSettings.MaxPowerLimit);
                    if (limit == s.PowerLimit) return;
                    s.PowerLimit = limit;
                    this.Changed();
                }
                this.App.Invalidate();
            }
        }

        static bool OnStep(int x, int left) => x >= left - 1 && x < left + StepW + 1;
    }
}
