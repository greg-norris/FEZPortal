using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// The pop-up shown over any screen while a countdown or alarm rings: Stop, and for the
    /// alarm also Snooze.
    /// </summary>
    public static class AlertOverlay {
        public const int None = 0, Stop = 1, Snooze = 2;

        const int X = 70, Y = 54, W = 340, H = 186;
        const int ButtonY = Y + H - 60, ButtonH = 44, ButtonW = 140;

        public static void Draw(Canvas c, PortalState s) {
            c.FillRoundRect(Theme.Accent, X - 3, Y - 3, W + 6, H + 6, 18);
            c.FillRoundRect(Theme.PanelDark, X, Y, W, H, 16);

            var cx = X + W / 2;
            if (s.AlertKind == PortalState.AlertAlarm) Icons.Bell(c, cx, Y + 32, 17, Theme.Accent);
            else Icons.Hourglass(c, cx, Y + 32, 17);
            c.DrawTextCentered(s.AlertTitle, UiFont.Huge, Theme.White, cx, Y + 54);
            c.DrawTextCentered(c.Ellipsize(s.AlertDetail, UiFont.Small, W - 30), UiFont.Small, Theme.TextMuted, cx, Y + 96);

            if (s.AlertKind == PortalState.AlertAlarm) {
                Widgets.Button(c, cx - ButtonW - 8, ButtonY, ButtonW, ButtonH, "Snooze", false, true);
                Widgets.Button(c, cx + 8, ButtonY, ButtonW, ButtonH, "Stop", true, true);
            }
            else {
                Widgets.Button(c, cx - ButtonW / 2, ButtonY, ButtonW, ButtonH, "Stop", true, true);
            }
        }

        /// <summary>Which button a tap hit (taps elsewhere are ignored while the pop-up is up).</summary>
        public static int Tap(PortalState s, int x, int y) {
            var cx = X + W / 2;
            if (y < ButtonY - 6 || y >= ButtonY + ButtonH + 6) return None;
            if (s.AlertKind == PortalState.AlertAlarm) {
                if (x >= cx - ButtonW - 14 && x < cx) return Snooze;
                if (x >= cx && x < cx + ButtonW + 14) return Stop;
                return None;
            }
            return x >= cx - ButtonW / 2 - 6 && x < cx + ButtonW / 2 + 6 ? Stop : None;
        }
    }
}
