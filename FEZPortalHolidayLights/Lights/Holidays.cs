namespace HolidayLights.Lights {
    /// <summary>
    /// One holiday light theme: a pattern from <see cref="Effects"/> played with the theme's own colours.
    /// </summary>
    public class HolidayTheme {
        // Patterns (see Effects.RenderTheme for what each one does).
        public const int Stripes = 0, Bulbs = 1, Sparkle = 2, Fade = 3, Drift = 4, Fireworks = 5, Heartbeat = 6, Flicker = 7, Lightning = 8, Wave = 9;

        public readonly int Holiday;
        public readonly string Name;
        /// <summary>The name in lights.txt (effect=...).</summary>
        public readonly string Key;
        public readonly int Pattern;
        /// <summary>The theme's colours as 0xRRGGBB, at full strength.</summary>
        public readonly int[] Colors;
        /// <summary>LEDs per colour band (Stripes, Bulbs, Drift, Wave, Heartbeat, Flicker).</summary>
        public readonly int Size;

        public HolidayTheme(int holiday, string name, string key, int pattern, int size, int[] colors) {
            this.Holiday = holiday;
            this.Name = name;
            this.Key = key;
            this.Pattern = pattern;
            this.Size = size;
            this.Colors = colors;
        }
    }

    /// <summary>
    /// The holidays on the Holidays page and the light themes for each. Themes are numbered after the
    /// main page's effects (effect = LightSettings.MainEffects + index into <see cref="Themes"/>).
    /// To add a theme, add a line below; up to four per holiday fit on a holiday's page.
    /// </summary>
    public static class Holidays {
        public const int NewYear = 0, Valentines = 1, StPatricks = 2, Easter = 3, July4 = 4, Halloween = 5, Thanksgiving = 6, Hanukkah = 7, Christmas = 8;

        public static readonly string[] Names = {
            "New Year's", "Valentine's Day", "St. Patrick's Day", "Easter", "Fourth of July",
            "Halloween", "Thanksgiving", "Hanukkah", "Christmas",
        };

        /// <summary>The colour dots under each holiday's name on the Holidays page.</summary>
        public static readonly int[][] Colors = {
            new[] { 0xFFB000, 0xFFFFFF, 0xC0C8D8 },
            new[] { 0xFF0000, 0xFF3080, 0xFFFFFF },
            new[] { 0x00C000, 0xFFFFFF, 0xFFB000 },
            new[] { 0xFF80B0, 0xFFE080, 0x80FFB0, 0x80C0FF },
            new[] { 0xFF0000, 0xFFFFFF, 0x2050FF },
            new[] { 0xFF5000, 0x8020FF, 0x40FF00 },
            new[] { 0xFF4000, 0xFFA000, 0xA02000 },
            new[] { 0x2050FF, 0xFFFFFF, 0xC0C8D8 },
            new[] { 0xFF0000, 0x00C000, 0xFFFFFF },
        };

        // Colours used below.
        const int Red = 0xFF0000, Green = 0x00C000, White = 0xFFFFFF, Blue = 0x0030FF, Gold = 0xFFA000;

        public static readonly HolidayTheme[] Themes = {
            new HolidayTheme(NewYear, "Fireworks", "newyear-fireworks", HolidayTheme.Fireworks, 0,
                new[] { 0xFFB000, White, 0xFF0040, 0x0060FF, 0x40FF40, 0xC000FF }),
            new HolidayTheme(NewYear, "Champagne", "newyear-champagne", HolidayTheme.Sparkle, 0,
                new[] { Gold, White, 0xFFD060 }),
            new HolidayTheme(NewYear, "Gold & silver", "newyear-goldsilver", HolidayTheme.Bulbs, 2,
                new[] { Gold, 0x90A0C0 }),
            new HolidayTheme(NewYear, "Midnight", "newyear-midnight", HolidayTheme.Drift, 14,
                new[] { 0x0010FF, 0x6000FF, 0x90A0FF }),

            new HolidayTheme(Valentines, "Heartbeat", "valentines-heartbeat", HolidayTheme.Heartbeat, 8,
                new[] { Red, 0xFF1060 }),
            new HolidayTheme(Valentines, "Sweethearts", "valentines-sweethearts", HolidayTheme.Drift, 12,
                new[] { Red, 0xFF0060, 0xFF80A0 }),
            new HolidayTheme(Valentines, "Love sparkle", "valentines-sparkle", HolidayTheme.Sparkle, 0,
                new[] { Red, White, 0xFF4090 }),
            new HolidayTheme(Valentines, "Pink & red", "valentines-pinkred", HolidayTheme.Stripes, 4,
                new[] { Red, 0xFF3080 }),

            new HolidayTheme(StPatricks, "Shamrock", "stpatricks-shamrock", HolidayTheme.Bulbs, 1,
                new[] { 0x00FF00, 0x00A020, 0x80FF00 }),
            new HolidayTheme(StPatricks, "Pot of gold", "stpatricks-potofgold", HolidayTheme.Sparkle, 0,
                new[] { Green, 0xFFB000, 0xFFD040 }),
            new HolidayTheme(StPatricks, "Irish flag", "stpatricks-flag", HolidayTheme.Wave, 6,
                new[] { 0x00C030, White, 0xFF5000 }),
            new HolidayTheme(StPatricks, "Clover drift", "stpatricks-drift", HolidayTheme.Drift, 12,
                new[] { 0x00FF00, 0x00A040, 0x60FF00 }),

            new HolidayTheme(Easter, "Pastels", "easter-pastels", HolidayTheme.Drift, 10,
                new[] { 0xFF6090, 0xFFD060, 0x60FF90, 0x60A0FF, 0xA060FF }),
            new HolidayTheme(Easter, "Easter eggs", "easter-eggs", HolidayTheme.Bulbs, 3,
                new[] { 0xFF6090, 0xFFD060, 0x60FF90, 0x60A0FF, 0xA060FF }),
            new HolidayTheme(Easter, "Spring sparkle", "easter-sparkle", HolidayTheme.Sparkle, 0,
                new[] { 0xA060FF, 0xFFD060, 0xFF6090, 0x60FF90 }),

            new HolidayTheme(July4, "Fireworks", "july4-fireworks", HolidayTheme.Fireworks, 0,
                new[] { Red, White, 0x0040FF }),
            new HolidayTheme(July4, "Flag", "july4-flag", HolidayTheme.Wave, 5,
                new[] { Red, White, Blue }),
            new HolidayTheme(July4, "Sparklers", "july4-sparklers", HolidayTheme.Sparkle, 0,
                new[] { 0x0020FF, White, Red }),
            new HolidayTheme(July4, "Parade", "july4-parade", HolidayTheme.Stripes, 4,
                new[] { Red, White, Blue }),

            new HolidayTheme(Halloween, "Pumpkin", "halloween-pumpkin", HolidayTheme.Flicker, 1,
                new[] { 0xFF2000, 0xFF5A00 }),
            new HolidayTheme(Halloween, "Spooky", "halloween-spooky", HolidayTheme.Drift, 14,
                new[] { 0xFF4000, 0x7000FF, 0x40FF00 }),
            new HolidayTheme(Halloween, "Lightning", "halloween-lightning", HolidayTheme.Lightning, 0,
                new[] { 0x5000C0, White }),
            new HolidayTheme(Halloween, "Candy corn", "halloween-candycorn", HolidayTheme.Stripes, 3,
                new[] { White, 0xFF4000, 0xFFB000 }),

            new HolidayTheme(Thanksgiving, "Autumn leaves", "thanksgiving-leaves", HolidayTheme.Drift, 12,
                new[] { 0xFF3000, 0xFF7000, 0xFFB000, 0xC00000 }),
            new HolidayTheme(Thanksgiving, "Harvest", "thanksgiving-harvest", HolidayTheme.Bulbs, 2,
                new[] { 0xFF4000, 0xFFA000, 0xC00000, 0xFF7000 }),
            new HolidayTheme(Thanksgiving, "Candlelight", "thanksgiving-candles", HolidayTheme.Flicker, 1,
                new[] { 0xFF5010, 0xFF9A4A }),

            new HolidayTheme(Hanukkah, "Blue & white", "hanukkah-bluewhite", HolidayTheme.Stripes, 5,
                new[] { Blue, White }),
            new HolidayTheme(Hanukkah, "Candles", "hanukkah-candles", HolidayTheme.Flicker, 1,
                new[] { 0xFF7010, 0xFFB050 }),
            new HolidayTheme(Hanukkah, "Starlight", "hanukkah-starlight", HolidayTheme.Sparkle, 0,
                new[] { Blue, White, 0x80C0FF }),
            new HolidayTheme(Hanukkah, "Festival", "hanukkah-festival", HolidayTheme.Fade, 0,
                new[] { Blue, 0x80C0FF, White }),

            new HolidayTheme(Christmas, "Candy cane", "christmas-candycane", HolidayTheme.Stripes, 5,
                new[] { Red, White }),
            new HolidayTheme(Christmas, "Red & green", "christmas-redgreen", HolidayTheme.Stripes, 4,
                new[] { Red, Green }),
            new HolidayTheme(Christmas, "Snowfall", "christmas-snowfall", HolidayTheme.Sparkle, 0,
                new[] { 0x0040FF, White }),
            new HolidayTheme(Christmas, "Christmas tree", "christmas-tree", HolidayTheme.Sparkle, 0,
                new[] { Green, Red, Gold, 0x0040FF, White }),
        };

        /// <summary>The effect numbers of a holiday's themes, in the order above.</summary>
        public static int[] EffectsOf(int holiday) {
            var n = 0;
            for (var i = 0; i < Themes.Length; i++) {
                if (Themes[i].Holiday == holiday) n++;
            }
            var list = new int[n];
            n = 0;
            for (var i = 0; i < Themes.Length; i++) {
                if (Themes[i].Holiday == holiday) list[n++] = LightSettings.MainEffects + i;
            }
            return list;
        }
    }
}
