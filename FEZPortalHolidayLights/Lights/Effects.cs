using System;

namespace HolidayLights.Lights {
    /// <summary>
    /// The light patterns. Each frame is drawn into an array of 0xRRGGBB colours at full strength;
    /// brightness, gamma and the power limit are applied afterwards by <see cref="StripOutput"/>.
    /// Time comes in as a phase (seconds at normal speed), so changing the speed never makes a
    /// pattern jump. Nothing here allocates, so frames don't keep the garbage collector busy.
    /// </summary>
    public class Effects {
        readonly int[] level;       // Twinkle: how lit each LED is (0-1024)
        uint seed = 0x9E3779B9;

        // Old-fashioned C9 bulb colours for Classic: red, green, blue, orange, gold.
        static readonly int[] ClassicColors = { 0xFF0000, 0x00C000, 0x0030FF, 0xFF5000, 0xFFB000 };

        // One cycle of a sine wave in 256 steps, scaled to -256..256 (whole numbers are much quicker
        // than Math.Sin on the Portal, which interprets the code).
        static readonly int[] Sine = MakeSine();

        static int[] MakeSine() {
            var table = new int[256];
            for (var i = 0; i < 256; i++) table[i] = (int)Math.Round(Math.Sin(i * Math.PI / 128) * 256);
            return table;
        }

        public Effects(int count) {
            this.level = new int[count];
            this.pick = new byte[count];
            for (var k = 0; k < MaxBursts; k++) this.burstAge[k] = -1;
        }

        /// <summary>True for effects that don't move: their frame only changes with the settings.</summary>
        public static bool IsStill(int effect) => effect == LightSettings.Solid;

        // Lookup tables: the Portal interprets the code, so a table read is far cheaper than a call,
        // a division or floating-point maths in the per-LED loops below.
        static readonly int[] HueTable = MakeHues();
        static readonly int[] ClassicR = Channel(ClassicColors, 16), ClassicG = Channel(ClassicColors, 8), ClassicB = Channel(ClassicColors, 0);

        static int[] MakeHues() {
            var table = new int[360];
            for (var i = 0; i < 360; i++) table[i] = Hue(i);
            return table;
        }

        static int[] Channel(int[] colors, int shift) {
            var table = new int[colors.Length];
            for (var i = 0; i < colors.Length; i++) table[i] = (colors[i] >> shift) & 0xFF;
            return table;
        }

        /// <summary>One frame of an effect into frame[0..count).</summary>
        /// <param name="rgb">The picked colour (used by Solid, Chase, Twinkle, Breathe and Comet).</param>
        /// <param name="phase">Seconds of animation so far, at normal speed.</param>
        /// <param name="dt">Animation seconds since the last frame (already multiplied by the speed).</param>
        public void Render(int effect, int rgb, int count, double phase, double dt, int[] frame) {
            // The picked colour's channels, for scaling it without a call per LED.
            int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;

            switch (effect) {
                case LightSettings.Solid:
                    for (var i = 0; i < count; i++) frame[i] = rgb;
                    break;

                case LightSettings.Rainbow: {
                    // Two rainbows along the strip, drifting forward: LED i gets hue i * 720 / count + shift,
                    // stepped along with a running quotient and remainder instead of a division per LED.
                    var shift = 360 - (int)(phase * 40) % 360;
                    var hues = HueTable;
                    int q = shift, rem = 0, qStep = 720 / count, rStep = 720 % count;
                    for (var i = 0; i < count; i++) {
                        while (q >= 360) q -= 360;
                        frame[i] = hues[q];
                        q += qStep;
                        rem += rStep;
                        if (rem >= count) {
                            rem -= count;
                            q++;
                        }
                    }
                    break;
                }

                case LightSettings.Chase: {
                    // Theatre marquee: every third LED lit, stepping along, on a faint glow.
                    var k = (3 - (int)(phase * 8) % 3) % 3;
                    var glow = Scale(rgb, 13);
                    for (var i = 0; i < count; i++) {
                        frame[i] = k == 0 ? rgb : glow;
                        if (++k == 3) k = 0;
                    }
                    break;
                }

                case LightSettings.Twinkle: {
                    // LEDs flash up at random and fade out over a dim base.
                    var fade = (int)(dt * 1.2 * 1024);
                    if (fade < 1 && dt > 0) fade = 1;
                    var chance = dt > 0 ? (uint)(dt * 0.35 * uint.MaxValue) : 0;
                    var levels = this.level;
                    var x = this.seed;
                    for (var i = 0; i < count; i++) {
                        var v = levels[i] - fade;
                        if (v < 0) v = 0;
                        // xorshift32, written out here to save a call per LED.
                        x ^= x << 13;
                        x ^= x >> 17;
                        x ^= x << 5;
                        if (x < chance) v = 1024;
                        levels[i] = v;
                        var f = 15 + (((v * v) >> 12) * 241 >> 8);
                        frame[i] = ((r * f >> 8) << 16) | ((g * f >> 8) << 8) | (b * f >> 8);
                    }
                    this.seed = x;
                    break;
                }

                case LightSettings.Breathe: {
                    // The whole strip slowly brightens and dims (one breath every 4 s).
                    var f = (int)(256 * (0.08 + 0.92 * (0.5 - 0.5 * Math.Cos(phase * Math.PI / 2))));
                    var c = Scale(rgb, f);
                    for (var i = 0; i < count; i++) frame[i] = c;
                    break;
                }

                case LightSettings.Comet: {
                    // A bright head with a fading tail runs along the strip (positions in 1/256 LED).
                    // Only the few LEDs under the tail are worked out; the rest are off.
                    for (var i = 0; i < count; i++) frame[i] = 0;
                    var tail = (count / 8 < 8 ? 8 : count / 8) * 256;
                    var cycle = count * 256 + tail;
                    var head = (int)(phase * 35 * 256 % cycle);
                    var first = (head - tail) / 256 - 1;
                    if (first < 0) first = 0;
                    var last = head / 256;
                    if (last >= count) last = count - 1;
                    for (var i = first; i <= last; i++) {
                        var d = head - i * 256;
                        if (d < 0 || d >= tail) continue;
                        var f = (tail - d) * 256 / tail;
                        f = f * f >> 8;
                        frame[i] = ((r * f >> 8) << 16) | ((g * f >> 8) << 8) | (b * f >> 8);
                    }
                    break;
                }

                case LightSettings.Classic: {
                    // Repeating bulb colours, each gently shimmering on its own.
                    var angle = (int)(phase * 1.6 * 128 / Math.PI) & 255;
                    var sine = Sine;
                    var bulb = 0;
                    for (var i = 0; i < count; i++) {
                        var f = 200 + sine[angle] * 56 / 256;
                        frame[i] = ((ClassicR[bulb] * f >> 8) << 16) | ((ClassicG[bulb] * f >> 8) << 8) | (ClassicB[bulb] * f >> 8);
                        angle = (angle + 94) & 255;
                        if (++bulb == 5) bulb = 0;
                    }
                    break;
                }

                default:
                    if (LightSettings.IsHoliday(effect) && effect < LightSettings.EffectNames.Length) {
                        this.RenderTheme(LightSettings.ThemeOf(effect), count, phase, dt, frame);
                    }
                    else {
                        for (var i = 0; i < count; i++) frame[i] = 0;
                    }
                    break;
            }
        }

        // -- Holiday themes ----------------------------------------------------------

        // The current theme's colours split into channels (set up when the theme changes).
        HolidayTheme theme;
        int[] pr, pg, pb;
        readonly byte[] pick;        // Sparkle: which colour each LED flashes in

        // Fireworks: up to MaxBursts at once; age < 0 means the slot is free.
        const int MaxBursts = 8;
        const double BurstLife = 1.4;
        readonly double[] burstAge = new double[MaxBursts];
        readonly int[] burstCenter = new int[MaxBursts], burstSize = new int[MaxBursts], burstColor = new int[MaxBursts];

        // Flicker: animation time since the LEDs last changed. Lightning: the strike now showing.
        double flickerTime;
        double strikeAge = -1;
        int strikeStart, strikeLength;

        void UseTheme(HolidayTheme t) {
            if (t == this.theme) return;
            var colors = t.Colors;
            this.pr = Channel(colors, 16);
            this.pg = Channel(colors, 8);
            this.pb = Channel(colors, 0);
            this.theme = t;
            // Sparkles, bursts and strikes from the last theme would be in its colours.
            var picks = this.pick;
            for (var i = 0; i < picks.Length; i++) picks[i] = 0;
            for (var k = 0; k < MaxBursts; k++) this.burstAge[k] = -1;
            this.strikeAge = -1;
        }

        uint Random() {
            var x = this.seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            this.seed = x;
            return x;
        }

        /// <summary>A random number from 0 to n - 1.</summary>
        int Random(int n) => (int)(this.Random() % (uint)n);

        void RenderTheme(HolidayTheme t, int count, double phase, double dt, int[] frame) {
            this.UseTheme(t);
            int[] pr = this.pr, pg = this.pg, pb = this.pb;
            var n = pr.Length;
            var size = t.Size < 1 ? 1 : t.Size;

            switch (t.Pattern) {
                case HolidayTheme.Stripes: {
                    // Bands of each colour, Size LEDs wide, sliding along (Candy cane: red and white, 5 wide).
                    var colors = t.Colors;
                    var block = size * n;
                    var k = (block - (int)(phase * 6) % block) % block;
                    int ci = k / size, sub = k % size;
                    for (var i = 0; i < count; i++) {
                        frame[i] = colors[ci];
                        if (++sub == size) {
                            sub = 0;
                            if (++ci == n) ci = 0;
                        }
                    }
                    break;
                }

                case HolidayTheme.Bulbs: {
                    // Like Classic: repeating bulb colours (Size LEDs each), each gently shimmering on its own.
                    var angle = (int)(phase * 1.6 * 128 / Math.PI) & 255;
                    var sine = Sine;
                    int ci = 0, sub = 0;
                    for (var i = 0; i < count; i++) {
                        var f = 200 + (sine[angle] * 56 >> 8);
                        frame[i] = ((pr[ci] * f >> 8) << 16) | ((pg[ci] * f >> 8) << 8) | (pb[ci] * f >> 8);
                        angle = (angle + 94) & 255;
                        if (++sub == size) {
                            sub = 0;
                            if (++ci == n) ci = 0;
                        }
                    }
                    break;
                }

                case HolidayTheme.Sparkle: {
                    // A soft glow of the first colour; LEDs flash up at random in the other colours and fade back.
                    var fade = (int)(dt * 1.1 * 1024);
                    if (fade < 1 && dt > 0) fade = 1;
                    var chance = dt > 0 ? (uint)(dt * 0.3 * uint.MaxValue) : 0;
                    int br = pr[0] * 110 >> 8, bg = pg[0] * 110 >> 8, bb = pb[0] * 110 >> 8;
                    var sparks = n > 1 ? n - 1 : 1;
                    var first = n > 1 ? 1 : 0;
                    var levels = this.level;
                    var picks = this.pick;
                    var x = this.seed;
                    for (var i = 0; i < count; i++) {
                        var v = levels[i] - fade;
                        if (v < 0) v = 0;
                        x ^= x << 13;
                        x ^= x >> 17;
                        x ^= x << 5;
                        if (x < chance) {
                            v = 1024;
                            picks[i] = (byte)(first + (int)((x >> 8) % (uint)sparks));
                        }
                        levels[i] = v;
                        if (v == 0) {
                            frame[i] = (br << 16) | (bg << 8) | bb;
                            continue;
                        }
                        var f = (v * v) >> 12;   // 0-256, quick to fade
                        var p = picks[i];
                        frame[i] = ((br + ((pr[p] - br) * f >> 8)) << 16) | ((bg + ((pg[p] - bg) * f >> 8)) << 8) | (bb + ((pb[p] - bb) * f >> 8));
                    }
                    this.seed = x;
                    break;
                }

                case HolidayTheme.Fade: {
                    // The whole strip holds each colour for a while, then fades smoothly into the next.
                    var p = phase * 0.3;
                    var whole = (int)p;
                    var frac = p - whole;
                    var a = whole % n;
                    var b = a + 1 == n ? 0 : a + 1;
                    var f = frac < 0.5 ? 0 : (int)(256 * Smooth((frac - 0.5) * 2));
                    var c = (Mix(pr[a], pr[b], f) << 16) | (Mix(pg[a], pg[b], f) << 8) | Mix(pb[a], pb[b], f);
                    for (var i = 0; i < count; i++) frame[i] = c;
                    break;
                }

                case HolidayTheme.Drift: {
                    // The colours blend into one another along the strip (Size LEDs from one to the next), drifting.
                    var total = n * 256;
                    var u = total - (int)(phase * 64) % total;
                    if (u == total) u = 0;
                    var step = 256 / size;
                    for (var i = 0; i < count; i++) {
                        var a = u >> 8;
                        var f = u & 255;
                        var b = a + 1 == n ? 0 : a + 1;
                        frame[i] = ((pr[a] + ((pr[b] - pr[a]) * f >> 8)) << 16) | ((pg[a] + ((pg[b] - pg[a]) * f >> 8)) << 8) | (pb[a] + ((pb[b] - pb[a]) * f >> 8));
                        u += step;
                        if (u >= total) u -= total;
                    }
                    break;
                }

                case HolidayTheme.Fireworks:
                    this.Fireworks(count, dt, frame);
                    break;

                case HolidayTheme.Heartbeat: {
                    // Two quick beats, then a rest (about 70 beats a minute at normal speed).
                    var beat = phase / 0.85;
                    var tt = beat - (int)beat;
                    var pulse = Bump(tt, 0, 0.16);
                    var second = 0.7 * Bump(tt, 0.2, 0.2);
                    if (second > pulse) pulse = second;
                    var f = (int)(256 * (0.3 + 0.7 * pulse));
                    int ci = 0, sub = 0;
                    var c = Scale((pr[0] << 16) | (pg[0] << 8) | pb[0], f);
                    for (var i = 0; i < count; i++) {
                        frame[i] = c;
                        if (++sub == size) {
                            sub = 0;
                            if (++ci == n) ci = 0;
                            c = Scale((pr[ci] << 16) | (pg[ci] << 8) | pb[ci], f);
                        }
                    }
                    break;
                }

                case HolidayTheme.Flicker: {
                    // Candle flames: each LED wavers on its own, brighter LEDs shifting towards the second colour.
                    this.flickerTime += dt;
                    var levels = this.level;
                    if (this.flickerTime >= 0.05) {
                        this.flickerTime = 0;
                        var x = this.seed;
                        for (var i = 0; i < count; i++) {
                            x ^= x << 13;
                            x ^= x >> 17;
                            x ^= x << 5;
                            var v = (levels[i] * 3 + 110 + (int)(x & 0x9F)) >> 2;
                            if ((x >> 24) < 3) v = 70;   // now and then a flame gutters
                            levels[i] = v > 256 ? 256 : v;
                        }
                        this.seed = x;
                    }
                    var hot = n > 1 ? 1 : 0;
                    for (var i = 0; i < count; i++) {
                        var v = levels[i];
                        var m = v > 128 ? (v - 128) * 2 : 0;
                        var r = pr[0] + ((pr[hot] - pr[0]) * m >> 8);
                        var g = pg[0] + ((pg[hot] - pg[0]) * m >> 8);
                        var b = pb[0] + ((pb[hot] - pb[0]) * m >> 8);
                        frame[i] = ((r * v >> 8) << 16) | ((g * v >> 8) << 8) | (b * v >> 8);
                    }
                    break;
                }

                case HolidayTheme.Lightning: {
                    // A slowly heaving glow of the first colour; now and then a stretch flashes in the second.
                    var breathe = (int)(256 * (0.45 + 0.25 * Math.Sin(phase * 0.9)));
                    var glow = Scale((pr[0] << 16) | (pg[0] << 8) | pb[0], breathe);
                    for (var i = 0; i < count; i++) frame[i] = glow;

                    if (this.strikeAge < 0) {
                        if (dt > 0 && this.Random() < (uint)(dt * 0.3 * uint.MaxValue)) {
                            this.strikeAge = 0;
                            this.strikeLength = count / 4 + this.Random(count / 2 + 1);
                            this.strikeStart = this.Random(count - this.strikeLength + 1);
                        }
                    }
                    else {
                        this.strikeAge += dt;
                        var a = this.strikeAge;
                        if (a > 0.5) this.strikeAge = -1;
                        else if (a < 0.06 || (a > 0.13 && a < 0.2) || (a > 0.3 && a < 0.5)) {
                            var flash = Scale((pr[n - 1] << 16) | (pg[n - 1] << 8) | pb[n - 1], a > 0.3 ? (int)(256 * (0.5 - a) / 0.2) : 256);
                            var end = this.strikeStart + this.strikeLength;
                            if (end > count) end = count;
                            for (var i = this.strikeStart; i < end; i++) frame[i] = flash;
                        }
                    }
                    break;
                }

                case HolidayTheme.Wave: {
                    // Bands of colour (Size LEDs each) like a flag, with a ripple of light running along it.
                    var angle = (int)(phase * 90) & 255;
                    var sine = Sine;
                    int ci = 0, sub = 0;
                    for (var i = 0; i < count; i++) {
                        var f = 150 + (sine[angle] * 106 >> 8);
                        frame[i] = ((pr[ci] * f >> 8) << 16) | ((pg[ci] * f >> 8) << 8) | (pb[ci] * f >> 8);
                        angle = (angle - 9) & 255;
                        if (++sub == size) {
                            sub = 0;
                            if (++ci == n) ci = 0;
                        }
                    }
                    break;
                }

                default:
                    for (var i = 0; i < count; i++) frame[i] = 0;
                    break;
            }
        }

        /// <summary>Bursts that flash white, spread out in a colour and fade, launched at random along the strip.</summary>
        void Fireworks(int count, double dt, int[] frame) {
            int[] pr = this.pr, pg = this.pg, pb = this.pb;
            for (var i = 0; i < count; i++) frame[i] = 0;

            // About two bursts a second on 160 LEDs, more on longer strips.
            var chance = dt * 2.0 * (count < 160 ? 1 : count / 160.0);
            if (chance > 0.95) chance = 0.95;
            if (dt > 0 && this.Random() < (uint)(chance * uint.MaxValue)) {
                for (var k = 0; k < MaxBursts; k++) {
                    if (this.burstAge[k] >= 0) continue;
                    this.burstAge[k] = 0;
                    this.burstCenter[k] = this.Random(count);
                    this.burstSize[k] = 6 + this.Random(9);
                    this.burstColor[k] = this.Random(pr.Length);
                    break;
                }
            }

            for (var k = 0; k < MaxBursts; k++) {
                var age = this.burstAge[k];
                if (age < 0) continue;
                age += dt;
                if (age >= BurstLife) {
                    this.burstAge[k] = -1;
                    continue;
                }
                this.burstAge[k] = age;
                var t = age / BurstLife;
                var center = this.burstCenter[k];
                var radius = (int)(this.burstSize[k] * (1 - (1 - t) * (1 - t) * (1 - t)) + 0.5);
                var f = (int)(256 * (1 - t));   // the strip's gamma makes this fade look quicker still
                var ci = this.burstColor[k];
                // The first moment: a white flash at the centre.
                var white = t < 0.08 ? 256 - (int)(t * 3200) : 0;
                for (var d = -radius; d <= radius; d++) {
                    var i = center + d;
                    if (i < 0 || i >= count) continue;
                    var dist = d < 0 ? -d : d;
                    // Brightest at the edge of the spreading ring.
                    var g = radius == 0 ? f : f * (80 + 176 * dist / radius) >> 8;
                    int r = pr[ci] * g >> 8, gr = pg[ci] * g >> 8, b = pb[ci] * g >> 8;
                    if (white > 0 && dist <= 1) {
                        r = r + ((255 - r) * white >> 8);
                        gr = gr + ((255 - gr) * white >> 8);
                        b = b + ((255 - b) * white >> 8);
                    }
                    // Overlapping bursts: keep the brighter of each channel.
                    var old = frame[i];
                    int oldR = (old >> 16) & 0xFF, oldG = (old >> 8) & 0xFF, oldB = old & 0xFF;
                    frame[i] = ((r > oldR ? r : oldR) << 16) | ((gr > oldG ? gr : oldG) << 8) | (b > oldB ? b : oldB);
                }
            }
        }

        /// <summary>A smooth bump from 0 up to 1 and back over [start, start + width), 0 elsewhere.</summary>
        static double Bump(double t, double start, double width) {
            var x = (t - start) / width;
            if (x <= 0 || x >= 1) return 0;
            var y = 4 * x * (1 - x);
            return y * y;
        }

        static double Smooth(double x) => x * x * (3 - 2 * x);

        static int Mix(int a, int b, int f) => a + ((b - a) * f >> 8);

        /// <summary>
        /// For the Setup page: the first 10 LEDs red, the next 10 green, the next 10 blue, so the colour
        /// order can be checked, and the last 10 white, to check the data reaches the far end and the
        /// LED count is right. The rest are off.
        /// </summary>
        public static void TestPattern(int count, int[] frame) {
            for (var i = 0; i < count; i++) {
                frame[i] = i < 10 ? 0xFF0000 : i < 20 ? 0x00FF00 : i < 30 ? 0x0000FF : i >= count - 10 ? 0xFFFFFF : 0;
            }
        }

        // -- Colour helpers --------------------------------------------------------

        /// <summary>A fully saturated colour for a hue in degrees (0 red, 120 green, 240 blue).</summary>
        public static int Hue(int degrees) {
            var h = Mod(degrees, 360);
            var sector = h / 60;
            var f = (h % 60) * 255 / 60;
            int r, g, b;
            switch (sector) {
                case 0: r = 255; g = f; b = 0; break;
                case 1: r = 255 - f; g = 255; b = 0; break;
                case 2: r = 0; g = 255; b = f; break;
                case 3: r = 0; g = 255 - f; b = 255; break;
                case 4: r = f; g = 0; b = 255; break;
                default: r = 255; g = 0; b = 255 - f; break;
            }
            return (r << 16) | (g << 8) | b;
        }

        /// <summary>A colour at f/256 of its strength.</summary>
        public static int Scale(int rgb, int f) {
            if (f <= 0) return 0;
            if (f >= 256) return rgb;
            return ((((rgb >> 16) & 0xFF) * f >> 8) << 16) | ((((rgb >> 8) & 0xFF) * f >> 8) << 8) | ((rgb & 0xFF) * f >> 8);
        }

        static int Mod(int a, int m) {
            var r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}
