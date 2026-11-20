using System.Drawing;

namespace HolidayLights.Ui {
    /// <summary>Device primitives: everything else in Canvas is built on these.</summary>
    public partial class Canvas {
        readonly Graphics screen;
        readonly Font[] fonts;
        SolidBrush brush;
        Color brushColor;

        public Canvas(Graphics screen) {
            this.screen = screen;
            // Same order as the UiFont enum.
            this.fonts = new[] {
                Resources.GetFont(Resources.FontResources.FontTiny),
                Resources.GetFont(Resources.FontResources.FontSmall),
                Resources.GetFont(Resources.FontResources.FontMedium),
                Resources.GetFont(Resources.FontResources.FontLarge),
                Resources.GetFont(Resources.FontResources.FontXLarge),
                Resources.GetFont(Resources.FontResources.FontHuge),
            };
        }

        public int Width => this.screen.Width;
        public int Height => this.screen.Height;

        public void FillRect(Color color, int x, int y, int w, int h) {
            if (w <= 0 || h <= 0) return;
            this.screen.FillRectangle(this.BrushFor(color), x, y, w, h);
        }

        public void DrawText(string text, UiFont font, Color color, int x, int y) =>
            this.screen.DrawString(text, this.fonts[(int)font], this.BrushFor(color), x, y);

        public int MeasureText(string text, UiFont font) =>
            (int)this.screen.MeasureString(text, this.fonts[(int)font]).Width;

        public int LineHeight(UiFont font) => this.fonts[(int)font].Height;

        /// <summary>Copies the whole drawing buffer to the display.</summary>
        public void Flush() => this.screen.Flush();

        /// <summary>Copies only part of the drawing buffer to the display.</summary>
        public void Flush(int x, int y, int w, int h) {
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > this.Width) w = this.Width - x;
            if (y + h > this.Height) h = this.Height - y;
            if (w > 0 && h > 0) this.screen.Flush(x, y, w, h);
        }

        // Shapes call FillRect thousands of times with the same colour; reuse the brush.
        SolidBrush BrushFor(Color color) {
            if (this.brush == null || this.brushColor != color) {
                this.brush = new SolidBrush(color);
                this.brushColor = color;
            }
            return this.brush;
        }
    }
}
