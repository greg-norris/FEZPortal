namespace HolidayLights.Lights {
    /// <summary>
    /// What the strip should do, plus the strip's own setup. The UI thread changes it and the light
    /// thread reads it, both inside lock(settings). Kept in HolidayLights\lights.txt on the SD card so
    /// the lights come back the same after a power cut.
    /// </summary>
    public class LightSettings {
        public const string FileName = "lights.txt";

        // Effects, in the order of the buttons on the main page; the holiday themes follow them
        // (effect MainEffects + i is Holidays.Themes[i]).
        public const int Solid = 0, Rainbow = 1, Chase = 2, Twinkle = 3, Breathe = 4, Comet = 5, Classic = 6;
        public const int MainEffects = 7;
        static readonly string[] MainNames = { "Solid", "Rainbow", "Chase", "Twinkle", "Breathe", "Comet", "Classic" };
        static readonly string[] MainKeys = { "solid", "rainbow", "chase", "twinkle", "breathe", "comet", "classic" };
        public static readonly string[] EffectNames = AllEffects(MainNames, false);
        static readonly string[] EffectKeys = AllEffects(MainKeys, true);

        static string[] AllEffects(string[] main, bool keys) {
            var themes = Holidays.Themes;
            var list = new string[main.Length + themes.Length];
            for (var i = 0; i < main.Length; i++) list[i] = main[i];
            for (var i = 0; i < themes.Length; i++) list[main.Length + i] = keys ? themes[i].Key : themes[i].Name;
            return list;
        }

        public static bool IsHoliday(int effect) => effect >= MainEffects;

        /// <summary>The holiday theme for an effect number (IsHoliday(effect) must be true).</summary>
        public static HolidayTheme ThemeOf(int effect) => Holidays.Themes[effect - MainEffects];

        /// <summary>Effects that use the picked colour (the others bring their own colours).</summary>
        public static bool UsesColor(int effect) =>
            effect == Solid || effect == Chase || effect == Twinkle || effect == Breathe || effect == Comet;

        // The picked colour: a hue (0-359) from the colour bar, or one of the whites.
        public const int WarmWhite = -1, CoolWhite = -2;

        /// <summary>Byte order the strip's chips expect. Most LPD8806 strips are GRB.</summary>
        public static readonly string[] Orders = { "GRB", "RGB", "BRG", "RBG", "GBR", "BGR" };

        public const int MaxCount = 512;
        public const int MinPowerLimit = 250, MaxPowerLimit = 10000, PowerLimitStep = 250;

        public bool On = true;
        public int Effect = Rainbow;
        public int Color = 0;               // hue 0-359, WarmWhite or CoolWhite
        public int Brightness = 40;         // 0-100
        public int Speed = 50;              // 0-100, 50 = normal
        public int Order;                   // index into Orders
        public int Count = 160;
        public int PowerLimit = 1000;       // mA the strip may draw (estimated)

        /// <summary>Goes up on every change, so the UI knows to redraw and the file to be saved.</summary>
        public int Version;

        SettingsFile file = new SettingsFile(DefaultText);

        public const string DefaultText =
            "# Holiday Lights settings. The Portal saves this file when you change something on the screen.\r\n" +
            "\r\n" +
            "# Number of LEDs: 160 for one 5 m GE32RGB8806A strip, 320 for two joined (also on the Setup page).\r\n" +
            "count=160\r\n" +
            "# Byte order the strip expects: GRB, RGB, BRG, RBG, GBR or BGR (also on the Setup page).\r\n" +
            "order=GRB\r\n" +
            "# Most current the strip may draw, in milliamps. Each LED draws up to 60 mA at full white,\r\n" +
            "# so 160 LEDs can need 9.6 A. Keep this below what your 5 V supply can deliver;\r\n" +
            "# the Portal dims the whole strip when a pattern would draw more.\r\n" +
            "powerlimit=1000\r\n" +
            "\r\n" +
            "# What the lights are doing (restored at power-up).\r\n" +
            "on=yes\r\n" +
            "# solid, rainbow, chase, twinkle, breathe, comet or classic, or a holiday theme such as\r\n" +
            "# christmas-candycane (the README lists them all).\r\n" +
            "effect=rainbow\r\n" +
            "# A hue from 0 to 359 (0 red, 120 green, 240 blue), or warm or white.\r\n" +
            "color=0\r\n" +
            "# 0 to 100\r\n" +
            "brightness=40\r\n" +
            "speed=50\r\n";

        /// <summary>Reads lights.txt (null = use the defaults).</summary>
        public void Load(string text) {
            if (text == null) text = DefaultText;
            var f = new SettingsFile(text);
            this.file = f;
            this.Count = Clamp(f.GetInt("count", 160), 1, MaxCount);
            this.Order = IndexOf(Orders, f.Get("order", "GRB").ToUpper(), 0);
            this.PowerLimit = Clamp(f.GetInt("powerlimit", 1000), MinPowerLimit, MaxPowerLimit);
            this.On = f.Get("on", "yes").ToLower() != "no";
            var effect = f.Get("effect", "rainbow").ToLower();
            if (effect == "candycane") effect = "christmas-candycane";   // on the main page before the holiday pages
            this.Effect = IndexOf(EffectKeys, effect, Rainbow);
            var color = f.Get("color", "0").ToLower();
            int hue;
            if (color == "warm") this.Color = WarmWhite;
            else if (color == "white") this.Color = CoolWhite;
            else this.Color = SettingsFile.ParseInt(color, out hue) ? ((hue % 360) + 360) % 360 : 0;
            this.Brightness = Clamp(f.GetInt("brightness", 40), 0, 100);
            this.Speed = Clamp(f.GetInt("speed", 50), 0, 100);
            this.Version++;
        }

        /// <summary>The lights.txt text for the current settings (the file's comments are kept).</summary>
        public string ToText() {
            var f = this.file;
            f.Set("count", this.Count.ToString());
            f.Set("order", Orders[this.Order]);
            f.Set("powerlimit", this.PowerLimit.ToString());
            f.Set("on", this.On ? "yes" : "no");
            f.Set("effect", EffectKeys[this.Effect]);
            f.Set("color", this.Color == WarmWhite ? "warm" : this.Color == CoolWhite ? "white" : this.Color.ToString());
            f.Set("brightness", this.Brightness.ToString());
            f.Set("speed", this.Speed.ToString());
            return f.ToText();
        }

        /// <summary>The picked colour as 0xRRGGBB.</summary>
        public int Rgb => ColorRgb(this.Color);

        public static int ColorRgb(int color) =>
            color == WarmWhite ? 0xFF9A4A : color == CoolWhite ? 0xFFFFFF : Effects.Hue(color);

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        static int IndexOf(string[] list, string value, int fallback) {
            for (var i = 0; i < list.Length; i++) {
                if (list[i] == value) return i;
            }
            return fallback;
        }
    }
}
