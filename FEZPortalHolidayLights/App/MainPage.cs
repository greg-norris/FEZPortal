using System.Drawing;
using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.App {
    /// <summary>
    /// The control page: effect buttons (the last one opens the Holidays page), a colour bar with warm
    /// and cool white, and brightness and speed sliders. On/off and Setup are in the status bar.
    /// Sliders and the colour bar follow a finger; each part redraws on its own so dragging stays smooth.
    /// </summary>
    public class MainPage : Page {
        // Setup, left of ON / OFF in the status bar.
        const int SetupX = 334, SetupW = 64;

        // Effect buttons: 4 x 2, the main effects and then Holidays.
        const int Cols = 4, EffectsY = 54, EffectW = 113, EffectH = 44, EffectGap = 6, EffectsX = 5;
        const int EffectsBottom = EffectsY + 2 * EffectH + EffectGap;

        // Colour bar and the two whites.
        const int ColorY = 156, ColorH = 40, HueX = 8, HueW = 330, WarmX = 346, WhiteX = 412, SwatchW = 60;

        const int HolidaysButton = LightSettings.MainEffects;

        // Parts to redraw on the next frame.
        const int BarPart = 1, EffectsPart = 2, ColorPart = 4, SlidersPart = 8;

        // What a finger that went down is dragging.
        const int NoDrag = 0, HueDrag = 1, BrightnessDrag = 2, SpeedDrag = 3;

        int dirtyParts;
        int drag;

        static readonly Color WarmColor = ToColor(LightSettings.ColorRgb(LightSettings.WarmWhite));

        public MainPage(LightsApp app) : base(app) { }

        public override void Opened() {
            this.drag = NoDrag;
        }

        // -- Drawing -----------------------------------------------------------------

        public override void Draw(Canvas c) {
            this.DrawBar(c);
            this.DrawEffects(c);
            this.DrawColors(c);
            this.DrawSliders(c);
            this.dirtyParts = 0;
        }

        public override void Frame(Canvas c) {
            if (this.BarOutOfDate()) this.dirtyParts |= BarPart;

            var parts = this.dirtyParts;
            if (parts == 0) return;
            this.dirtyParts = 0;
            if ((parts & BarPart) != 0) {
                this.DrawBar(c);
                c.Flush(0, 0, Theme.ScreenWidth, Theme.BarHeight);
            }
            if ((parts & EffectsPart) != 0) {
                this.DrawEffects(c);
                c.Flush(0, EffectsY, Theme.ScreenWidth, EffectsBottom - EffectsY);
            }
            if ((parts & ColorPart) != 0) {
                this.DrawColors(c);
                c.Flush(0, ColorY - 4, Theme.ScreenWidth, ColorH + 8);
            }
            if ((parts & SlidersPart) != 0) {
                this.DrawSliders(c);
                FlushSliders(c);
            }
        }

        void DrawBar(Canvas c) {
            this.DrawBar(c, "Holiday Lights", false);
            BarButton(c, SetupX, SetupW, "Setup", Theme.PanelLight, Theme.White);
        }

        void DrawEffects(Canvas c) {
            c.FillRect(Theme.Background, 0, EffectsY, Theme.ScreenWidth, EffectsBottom - EffectsY);
            var effect = this.Settings.Effect;
            for (var i = 0; i < LightSettings.MainEffects; i++) {
                Button(c, EffectX(i % Cols), EffectY(i / Cols), EffectW, EffectH, LightSettings.EffectNames[i], i == effect, UiFont.Large);
            }
            // Holidays: yellow while one of the holiday themes is on.
            Button(c, EffectX(HolidaysButton % Cols), EffectY(HolidaysButton / Cols), EffectW, EffectH, "Holidays",
                LightSettings.IsHoliday(effect), UiFont.Large);
        }

        static int EffectX(int col) => EffectsX + col * (EffectW + EffectGap);
        static int EffectY(int row) => EffectsY + row * (EffectH + EffectGap);

        void DrawColors(Canvas c) {
            var s = this.Settings;
            // Effects with their own colours show the picker faded.
            var fade = LightSettings.UsesColor(s.Effect) ? 0 : 0.6;
            c.FillRect(Theme.Background, 0, ColorY - 4, Theme.ScreenWidth, ColorH + 8);

            for (var i = 0; i < HueW; i++) {
                var color = ToColor(Effects.Hue(i * 360 / HueW));
                if (fade > 0) color = Canvas.Lerp(color, Theme.Background, fade);
                // Rounded ends: shorten the outer columns.
                var inset = i < 4 ? 4 - i : i >= HueW - 4 ? i - (HueW - 5) : 0;
                c.FillRect(color, HueX + i, ColorY + inset, 1, ColorH - inset * 2);
            }

            this.Swatch(c, WarmX, "Warm", WarmColor, s.Color == LightSettings.WarmWhite, fade);
            this.Swatch(c, WhiteX, "White", Theme.White, s.Color == LightSettings.CoolWhite, fade);

            if (fade == 0 && s.Color >= 0) {
                // Handle on the picked hue.
                var hx = HueX + s.Color * HueW / 360;
                c.FillRoundRect(Theme.Background, hx - 4, ColorY - 4, 9, ColorH + 8, 3);
                c.FillRoundRect(Theme.White, hx - 2, ColorY - 2, 5, ColorH + 4, 2);
            }
        }

        void Swatch(Canvas c, int x, string label, Color color, bool selected, double fade) {
            if (fade > 0) color = Canvas.Lerp(color, Theme.Background, fade);
            if (selected && fade == 0) c.FillRoundRect(Theme.Accent, x - 3, ColorY - 3, SwatchW + 6, ColorH + 6, Theme.Radius + 2);
            c.FillRoundRect(color, x, ColorY, SwatchW, ColorH, Theme.Radius);
            c.DrawTextCentered(label, UiFont.Medium, fade > 0 ? Theme.TextDim : Theme.PanelDark, x + SwatchW / 2, ColorY + (ColorH - c.LineHeight(UiFont.Medium)) / 2);
        }

        static Color ToColor(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        // -- Touch -------------------------------------------------------------------

        public override void Down(int x, int y) {
            this.drag = NoDrag;

            if (y < Theme.BarHeight) {
                if (this.PowerTap(x)) {
                    this.dirtyParts |= BarPart;
                }
                else if (x >= SetupX - 6 && x < SetupX + SetupW + 4) {
                    this.App.Show(this.App.Setup);
                }
                return;
            }

            // Effect buttons.
            if (y >= EffectsY && y < EffectsBottom) {
                var col = (x - EffectsX) / (EffectW + EffectGap);
                var row = (y - EffectsY) / (EffectH + EffectGap);
                if (col < 0 || col >= Cols || !Inside(x, y, EffectX(col), EffectY(row), EffectW, EffectH)) return;
                var effect = row * Cols + col;
                if (effect == HolidaysButton) {
                    this.App.Show(this.App.HolidayMenu);
                    return;
                }
                if (effect > HolidaysButton) return;
                this.PickEffect(effect);   // picking an effect turns the lights on
                this.dirtyParts |= EffectsPart | ColorPart | BarPart;
                return;
            }

            // Colour bar and whites.
            if (y >= ColorY - 8 && y < ColorY + ColorH + 6) {
                if (x >= HueX - 6 && x < HueX + HueW + 4) {
                    this.drag = HueDrag;
                    this.SetHue(x);
                }
                else if (x >= WarmX - 3 && x < WarmX + SwatchW + 3) {
                    this.SetColor(LightSettings.WarmWhite);
                }
                else if (x >= WhiteX - 3) {
                    this.SetColor(LightSettings.CoolWhite);
                }
                return;
            }

            // Sliders: anywhere on the row, so a finger needn't hit the knob.
            if (y >= BrightnessY && y < SpeedY) {
                this.drag = BrightnessDrag;
                this.Slide(x);
            }
            else if (y >= SpeedY) {
                this.drag = SpeedDrag;
                this.Slide(x);
            }
        }

        public override void Move(int x, int y) {
            if (this.drag == HueDrag) this.SetHue(x);
            else if (this.drag != NoDrag) this.Slide(x);
        }

        public override void Up(int x, int y) {
            this.drag = NoDrag;
        }

        void SetHue(int x) => this.SetColor(LightSettings.Clamp((x - HueX) * 360 / HueW, 0, 359));

        /// <summary>Picks a colour; with an effect that brings its own colours, switches to Solid to show it.</summary>
        void SetColor(int color) {
            var s = this.Settings;
            lock (s) {
                if (s.Color == color && LightSettings.UsesColor(s.Effect) && s.On) return;
                s.Color = color;
                if (!LightSettings.UsesColor(s.Effect)) {
                    s.Effect = LightSettings.Solid;
                    this.dirtyParts |= EffectsPart;
                }
                if (!s.On) {
                    s.On = true;
                    this.dirtyParts |= BarPart;
                }
                this.Changed();
            }
            this.dirtyParts |= ColorPart;
        }

        void Slide(int x) {
            if (this.SetSlider(this.drag == BrightnessDrag, x)) this.dirtyParts |= SlidersPart;
        }
    }
}
