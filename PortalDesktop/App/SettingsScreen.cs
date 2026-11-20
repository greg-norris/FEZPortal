using System.Drawing;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Opened by tapping "FEZ Portal Desktop" in the status bar: the light/dark theme choice
    /// (saved in Portal\display.txt) and a few lines about the Portal's state.
    /// </summary>
    public class SettingsScreen : Screen {
        const int CardX = Theme.Margin, CardW = Theme.ScreenWidth - Theme.Margin * 2;
        const int ThemeY = 38, ThemeH = 168;
        const int TileY = ThemeY + 30, TileW = 214, TileH = 128, TileGap = 12;
        const int AboutY = 212, AboutH = 52;

        public SettingsScreen(Shell shell) : base(shell) { }

        public override string Title => "Settings";

        public override int Stamp => this.State.StatusVersion + (this.State.Config.DarkTheme ? 100003 : 0);

        static int TileX(int i) => CardX + (CardW - TileW * 2 - TileGap) / 2 + i * (TileW + TileGap);

        public override void Draw(Canvas c) {
            Card(c, CardX, ThemeY, CardW, ThemeH);
            c.DrawText("Theme", UiFont.Medium, Theme.White, CardX + 14, ThemeY + 8);
            var dark = this.State.Config.DarkTheme;
            DrawTile(c, TileX(0), "Light", Theme.LightBackground, !dark);
            DrawTile(c, TileX(1), "Dark", Theme.DarkBackground, dark);
            this.DrawAbout(c);
        }

        /// <summary>A theme choice: a small picture of the screen in that theme, and its name.</summary>
        static void DrawTile(Canvas c, int x, string name, Color background, bool selected) {
            c.FillRoundRect(selected ? Theme.Accent : Theme.PanelLight, x, TileY, TileW, TileH, 10);
            c.FillRoundRect(Theme.PanelDark, x + 3, TileY + 3, TileW - 6, TileH - 6, 8);

            // Miniature screen: status bar, then a wide card and three small ones.
            int sx = x + 12, sy = TileY + 10, sw = TileW - 24, sh = 80;
            c.FillRoundRect(background, sx, sy, sw, sh, 6);
            c.FillRect(Theme.PanelDark, sx + 4, sy + 1, sw - 8, 10);
            c.FillRect(Theme.PanelDark, sx, sy + 4, sw, 7);
            c.FillRoundRect(Theme.Accent, sx + 5, sy + 3, 6, 6, 2);
            c.FillRect(Theme.White, sx + 14, sy + 5, 34, 2);
            c.FillRoundRect(Theme.Panel, sx + 6, sy + 16, sw - 12, 28, 4);
            c.FillRect(Theme.White, sx + 12, sy + 23, 46, 6);
            c.FillRect(Theme.TextMuted, sx + 12, sy + 33, 70, 3);
            var smallW = (sw - 12 - 8) / 3;
            for (var i = 0; i < 3; i++) c.FillRoundRect(Theme.Panel, sx + 6 + i * (smallW + 4), sy + 49, smallW, 25, 4);

            // Radio button and name.
            var ry = TileY + TileH - 22;
            c.FillCircle(selected ? Theme.Accent : Theme.TextMuted, x + 22, ry, 8);
            c.FillCircle(Theme.PanelDark, x + 22, ry, 6);
            if (selected) c.FillCircle(Theme.Accent, x + 22, ry, 3);
            c.DrawText(name, UiFont.Large, Theme.White, x + 38, ry - 10);
        }

        void DrawAbout(Canvas c) {
            var s = this.State;
            Card(c, CardX, AboutY, CardW, AboutH);
            c.DrawText("FEZ Portal Desktop", UiFont.Medium, Theme.White, CardX + 14, AboutY + 8);
            var wifi = s.Wifi == WifiStatus.Connected ? "Wi-Fi " + s.WifiSsid + (s.IpAddress.Length > 0 ? " (" + s.IpAddress + ")" : "") : s.WifiMessage;
            var line = (s.SdPresent ? "SD card ready" : "No SD card") + "  ·  " + wifi;
            c.DrawText(c.Ellipsize(line, UiFont.Tiny, CardW - 28), UiFont.Tiny, Theme.TextMuted, CardX + 14, AboutY + 30);
        }

        public override void Tap(int x, int y) {
            if (y < TileY || y >= TileY + TileH) return;
            for (var i = 0; i < 2; i++) {
                if (x < TileX(i) || x >= TileX(i) + TileW) continue;
                var dark = i == 1;
                if (dark == this.State.Config.DarkTheme) return;
                // Saved when there is a card; without one it lasts until restart.
                this.Shell.Host.SaveSettings(DefaultFiles.DisplayName, this.State.Config.SetDarkTheme(dark));
                this.Shell.Invalidate();
                return;
            }
        }
    }
}
