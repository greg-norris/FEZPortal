using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.App {
    /// <summary>
    /// One holiday's light themes (up to four, 2 x 2), each with its colours under its name, and the
    /// brightness and speed sliders. Tapping a theme starts it; the one that is on shows in yellow.
    /// </summary>
    public class ThemesPage : Page {
        const int Cols = 2, GridX = 6, GridY = 54, ButtonW = 231, ButtonH = 64, Gap = 6;
        const int GridBottom = GridY + 2 * ButtonH + Gap;

        // Parts to redraw on the next frame.
        const int BarPart = 1, ThemesPart = 2, SlidersPart = 4;

        // What a finger that went down is dragging.
        const int NoDrag = 0, BrightnessDrag = 1, SpeedDrag = 2;

        int holiday;
        int[] themes = new int[0];   // effect numbers
        int dirtyParts;
        int drag;

        public ThemesPage(LightsApp app) : base(app) { }

        /// <summary>The holiday shown (an index into Holidays.Names).</summary>
        public int Holiday => this.holiday;

        /// <summary>Shows the themes for a holiday.</summary>
        public void Open(int holiday) {
            this.holiday = holiday;
            this.themes = Holidays.EffectsOf(holiday);
            this.App.Show(this);
        }

        public override void Opened() {
            this.drag = NoDrag;
        }

        // -- Drawing -----------------------------------------------------------------

        string Title => Holidays.Names[this.holiday];

        public override void Draw(Canvas c) {
            this.DrawBar(c, this.Title, true);
            this.DrawThemes(c);
            this.DrawSliders(c);
            this.dirtyParts = 0;
        }

        void DrawThemes(Canvas c) {
            c.FillRect(Theme.Background, 0, GridY, Theme.ScreenWidth, GridBottom - GridY);
            var effect = this.Settings.Effect;
            for (var i = 0; i < this.themes.Length && i < 2 * Cols; i++) {
                int x = ButtonX(i), y = ButtonY(i);
                var selected = this.themes[i] == effect;
                var theme = LightSettings.ThemeOf(this.themes[i]);
                c.FillRoundRect(selected ? Theme.Accent : Theme.Panel, x, y, ButtonW, ButtonH, Theme.Radius);
                c.DrawTextCentered(theme.Name, UiFont.Large, selected ? Theme.PanelDark : Theme.White, x + ButtonW / 2, y + 11);
                // Fireworks, sparkles and the like show their colours once each; stripes and bands repeat them.
                var colors = theme.Colors;
                var p = theme.Pattern;
                var banded = p == HolidayTheme.Stripes || p == HolidayTheme.Bulbs || p == HolidayTheme.Wave;
                var dots = banded && colors.Length < 4 ? colors.Length * (6 / colors.Length) : colors.Length;
                Dots(c, colors, dots, x + ButtonW / 2, y + 45);
            }
        }

        static int ButtonX(int i) => GridX + i % Cols * (ButtonW + Gap);
        static int ButtonY(int i) => GridY + i / Cols * (ButtonH + Gap);

        public override void Frame(Canvas c) {
            if (this.BarOutOfDate()) this.dirtyParts |= BarPart;
            var parts = this.dirtyParts;
            if (parts == 0) return;
            this.dirtyParts = 0;
            if ((parts & BarPart) != 0) {
                this.DrawBar(c, this.Title, true);
                c.Flush(0, 0, Theme.ScreenWidth, Theme.BarHeight);
            }
            if ((parts & ThemesPart) != 0) {
                this.DrawThemes(c);
                c.Flush(0, GridY, Theme.ScreenWidth, GridBottom - GridY);
            }
            if ((parts & SlidersPart) != 0) {
                this.DrawSliders(c);
                FlushSliders(c);
            }
        }

        // -- Touch -------------------------------------------------------------------

        public override void Down(int x, int y) {
            this.drag = NoDrag;
            if (y < Theme.BarHeight) {
                if (this.PowerTap(x)) this.dirtyParts |= BarPart;
                else if (x < BackW) this.App.Show(this.App.HolidayMenu);
                return;
            }

            if (y < GridBottom + Gap) {
                for (var i = 0; i < this.themes.Length && i < 2 * Cols; i++) {
                    if (Inside(x, y, ButtonX(i) - Gap / 2, ButtonY(i) - Gap / 2, ButtonW + Gap, ButtonH + Gap)) {
                        this.PickEffect(this.themes[i]);
                        this.dirtyParts |= ThemesPart | BarPart;
                        return;
                    }
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
            if (this.drag != NoDrag) this.Slide(x);
        }

        public override void Up(int x, int y) {
            this.drag = NoDrag;
        }

        void Slide(int x) {
            if (this.SetSlider(this.drag == BrightnessDrag, x)) this.dirtyParts |= SlidersPart;
        }
    }
}
