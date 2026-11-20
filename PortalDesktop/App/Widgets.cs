using System.Drawing;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>Buttons and arrows shared by the clock tool pages.</summary>
    public static class Widgets {
        public const int ArrowW = 52, ArrowH = 24;

        /// <summary>A rounded button: yellow for the main action, dark blue otherwise, grey text when disabled.</summary>
        public static void Button(Canvas c, int x, int y, int w, int h, string label, bool primary, bool enabled) {
            var bg = primary && enabled ? Theme.Accent : Theme.Panel;
            var fg = primary && enabled ? Theme.PanelDark : enabled ? Theme.White : Theme.TextDim;
            c.FillRoundRect(bg, x, y, w, h, 12);
            c.DrawTextCentered(label, UiFont.XLarge, fg, x + w / 2, y + (h - c.LineHeight(UiFont.XLarge)) / 2);
        }

        /// <summary>Small up/down arrow button centred on (cx, cy), used above and below digits.</summary>
        public static void Arrow(Canvas c, int cx, int cy, bool up) => Arrow(c, cx, cy, up, ArrowW);

        public static void Arrow(Canvas c, int cx, int cy, bool up, int w) {
            c.FillRoundRect(Theme.PanelLight, cx - w / 2, cy - ArrowH / 2, w, ArrowH, 8);
            if (up) c.FillTriangle(Theme.White, cx - 8, cy + 4, cx + 8, cy + 4, cx, cy - 5);
            else c.FillTriangle(Theme.White, cx - 8, cy - 4, cx + 8, cy - 4, cx, cy + 5);
        }

        /// <summary>Whether a tap lands on an arrow centred on (cx, cy) (a little generously).</summary>
        public static bool OnArrow(int x, int y, int cx, int cy) => OnArrow(x, y, cx, cy, ArrowW);

        public static bool OnArrow(int x, int y, int cx, int cy, int w) =>
            x >= cx - w / 2 - 3 && x < cx + w / 2 + 3 && y >= cy - ArrowH / 2 - 6 && y < cy + ArrowH / 2 + 6;

        /// <summary>A thin progress bar; fraction 0..1 is filled.</summary>
        public static void Progress(Canvas c, int x, int y, int w, double fraction, Color color) {
            c.FillRoundRect(Theme.PanelLight, x, y, w, 6, 3);
            if (fraction <= 0) return;
            if (fraction > 1) fraction = 1;
            var fw = (int)(w * fraction);
            if (fw >= 6) c.FillRoundRect(color, x, y, fw, 6, 3);
        }
    }
}
