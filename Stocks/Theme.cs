namespace Stocks;

/// <summary>Colors as 0xRRGGBB (what the UI widgets take). A dark theme; the panel is RGB565.</summary>
static class Theme {
    public const uint Background = 0x0D1117;
    public const uint Header = 0x0D1117;
    public const uint Divider = 0x30363D;
    public const uint Card = 0x161B22;
    public const uint CardPressed = 0x58A6FF;     // outline while a finger is on a card
    public const uint Track = 0x30363D;           // range bars
    public const uint Grid = 0x21262D;            // chart grid lines

    public const uint Text = 0xE6EDF3;
    public const uint SubText = 0x8B949E;
    public const uint Faint = 0x6E7681;
    public const uint Accent = 0x58A6FF;

    public const uint Up = 0x3FB950;
    public const uint Down = 0xF85149;
    public const uint Warning = 0xD29922;

    public static uint UpDown(bool up) => up ? Up : Down;

    /// <summary>Mix of <paramref name="a"/> and <paramref name="b"/>; <paramref name="t"/> = 0 gives a, 1 gives b.</summary>
    public static uint Blend(uint a, uint b, double t) {
        uint Mix(int shift) {
            var ca = (a >> shift) & 0xFF;
            var cb = (b >> shift) & 0xFF;
            return (uint)Math.Round(ca + (cb - (double)ca) * t) << shift;
        }
        return Mix(16) | Mix(8) | Mix(0);
    }
}
