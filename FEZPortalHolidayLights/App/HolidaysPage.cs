using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.App {
    /// <summary>
    /// The Holidays page: a button per holiday (3 x 3), each with its colours under its name. Tapping
    /// one opens that holiday's themes. The holiday whose theme is on shows in yellow.
    /// </summary>
    public class HolidaysPage : Page {
        const int Cols = 3, GridX = 6, GridY = 54, ButtonW = 152, ButtonH = 66, Gap = 6;

        public HolidaysPage(LightsApp app) : base(app) { }

        public override void Draw(Canvas c) {
            this.DrawBar(c, "Holidays", true);
            var effect = this.Settings.Effect;
            var on = LightSettings.IsHoliday(effect) ? LightSettings.ThemeOf(effect).Holiday : -1;
            for (var h = 0; h < Holidays.Names.Length; h++) {
                int x = ButtonX(h), y = ButtonY(h);
                var selected = h == on;
                c.FillRoundRect(selected ? Theme.Accent : Theme.Panel, x, y, ButtonW, ButtonH, Theme.Radius);
                c.DrawTextCentered(Holidays.Names[h], UiFont.Large, selected ? Theme.PanelDark : Theme.White, x + ButtonW / 2, y + 12);
                var colors = Holidays.Colors[h];
                Dots(c, colors, colors.Length, x + ButtonW / 2, y + 47);
            }
        }

        static int ButtonX(int h) => GridX + h % Cols * (ButtonW + Gap);
        static int ButtonY(int h) => GridY + h / Cols * (ButtonH + Gap);

        public override void Frame(Canvas c) {
            if (!this.BarOutOfDate()) return;
            this.DrawBar(c, "Holidays", true);
            c.Flush(0, 0, Theme.ScreenWidth, Theme.BarHeight);
        }

        public override void Down(int x, int y) {
            if (y < Theme.BarHeight) {
                if (this.PowerTap(x)) this.App.Invalidate();
                else if (x < BackW) this.App.Show(this.App.Main);
                return;
            }
            for (var h = 0; h < Holidays.Names.Length; h++) {
                if (Inside(x, y, ButtonX(h) - Gap / 2, ButtonY(h) - Gap / 2, ButtonW + Gap, ButtonH + Gap)) {
                    this.App.ThemeMenu.Open(h);
                    return;
                }
            }
        }
    }
}
