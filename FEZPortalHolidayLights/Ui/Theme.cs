using System.Drawing;

namespace HolidayLights.Ui {
    /// <summary>
    /// Fonts available to the UI. Each maps to a Resources\Font{Name}.tcfnt resource (the same files
    /// as PortalDesktop, made by tools\make_ui_fonts.py; keep the order in sync with Canvas.Device.cs
    /// and the preview).
    /// </summary>
    public enum UiFont {
        Tiny,    // Tahoma Bold 11px
        Small,   // Tahoma Bold 12px
        Medium,  // Tahoma Bold 14px
        Large,   // Arial Bold 16px
        XLarge,  // Arial Bold 20px
        Huge,    // Arial Bold 32px
    }

    /// <summary>Colours and layout shared by every page (the PortalDesktop palette, on a dark background).</summary>
    public static class Theme {
        public static readonly Color Background = Color.FromArgb(8, 13, 30);
        public static readonly Color Panel = Color.FromArgb(27, 50, 117);
        public static readonly Color PanelDark = Color.FromArgb(19, 36, 88);
        public static readonly Color PanelLight = Color.FromArgb(44, 72, 150);
        public static readonly Color Accent = Color.FromArgb(248, 195, 44);
        public static readonly Color AccentSoft = Color.FromArgb(142, 195, 238);
        public static readonly Color Track = Color.FromArgb(74, 115, 196);
        public static readonly Color White = Color.FromArgb(255, 255, 255);
        public static readonly Color TextMuted = Color.FromArgb(200, 218, 242);
        public static readonly Color TextDim = Color.FromArgb(140, 165, 210);
        public static readonly Color Up = Color.FromArgb(76, 217, 123);
        public static readonly Color Down = Color.FromArgb(255, 107, 107);

        public const int ScreenWidth = 480;
        public const int ScreenHeight = 272;
        public const int BarHeight = 30;
        public const int Margin = 8;
        public const int Radius = 10;
    }
}
