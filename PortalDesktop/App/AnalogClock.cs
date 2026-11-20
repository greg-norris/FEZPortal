using System;
using System.Drawing;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// The round analog clock (ticks, 3/6/9/12, hour, minute and seconds hands), drawn at any
    /// size. Used on the Clock page and the full-screen clock.
    /// </summary>
    public static class AnalogClock {
        /// <summary>Clears the square behind the face and draws it fresh (this also erases the old hands).</summary>
        public static void Draw(Canvas c, int cx, int cy, int r, DateTime now) {
            c.FillRect(Theme.Background, cx - r, cy - r, r * 2 + 1, r * 2 + 1);
            c.FillCircle(Theme.Panel, cx, cy, r);
            c.DrawCircle(Theme.Accent, cx, cy, r, 3);

            // Sizes scale with the radius (designed at r = 108).
            var minuteDot = Scale(r, 12);
            var hourIn = Scale(r, 22);
            var numbers = Scale(r, 40);

            for (var m = 0; m < 60; m++) {
                if (m % 5 == 0) continue;
                c.FillRect(Theme.TextDim, Canvas.PolarX(cx, r - minuteDot, m * 6) - 1, Canvas.PolarY(cy, r - minuteDot, m * 6) - 1, 2, 2);
            }
            for (var h = 0; h < 12; h++) {
                var a = h * 30;
                c.ThickLine(h % 3 == 0 ? Theme.Accent : Theme.White,
                    Canvas.PolarX(cx, r - hourIn, a), Canvas.PolarY(cy, r - hourIn, a),
                    Canvas.PolarX(cx, r - minuteDot, a), Canvas.PolarY(cy, r - minuteDot, a), 4);
            }
            var font = r >= 90 ? UiFont.XLarge : UiFont.Large;
            var lh = c.LineHeight(font);
            for (var h = 3; h <= 12; h += 3) {
                var a = h * 30;
                c.DrawTextCentered(h.ToString(), font, Theme.White, Canvas.PolarX(cx, r - numbers, a), Canvas.PolarY(cy, r - numbers, a) - lh / 2);
            }

            var second = now.Second * 6.0;
            var minute = now.Minute * 6.0 + now.Second / 10.0;
            var hour = (now.Hour % 12) * 30.0 + now.Minute / 2.0;
            Hand(c, cx, cy, hour, r * 0.5, 0, Math.Max(4, Scale(r, 7)), Theme.White);
            Hand(c, cx, cy, minute, r * 0.76, 0, Math.Max(3, Scale(r, 5)), Theme.White);
            Hand(c, cx, cy, second, r * 0.84, r * 0.16, 2, Theme.Accent);
            c.FillCircle(Theme.Accent, cx, cy, Math.Max(4, Scale(r, 6)));
            c.FillCircle(Theme.PanelDark, cx, cy, 2);
        }

        static int Scale(int r, int atDesign) => (atDesign * r + 54) / 108;

        static void Hand(Canvas c, int cx, int cy, double angle, double length, double tail, int width, Color color) {
            c.ThickLine(color,
                Canvas.PolarX(cx, tail, angle + 180), Canvas.PolarY(cy, tail, angle + 180),
                Canvas.PolarX(cx, length, angle), Canvas.PolarY(cy, length, angle), width);
        }
    }
}
