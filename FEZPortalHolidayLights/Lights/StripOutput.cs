namespace HolidayLights.Lights {
    /// <summary>
    /// Turns a frame of colours into the bytes an LPD8806 strip expects over SPI:
    /// zero bytes to reset the chips, then three bytes per LED in the strip's colour order, each
    /// 0x80 | a 7-bit level, then (count + 31) / 32 zero bytes to latch the new colours.
    /// Applies the brightness (before gamma, so the slider feels even) and the power limit.
    ///
    /// The Portal interprets the code, so the per-byte work is a single table read: the table maps an
    /// 8-bit colour level straight to the byte to send at the current brightness, and is rebuilt only
    /// when the brightness changes. Not thread-safe: only the light thread (or the preview) calls it.
    /// </summary>
    public static class StripOutput {
        /// <summary>Current one colour channel of one LED draws at full level.</summary>
        public const int ChannelMilliamps = 20;

        // 8-bit level -> 7-bit LED level with a gamma of 2, so dim colours don't look washed out.
        static readonly byte[] Gamma = MakeGamma();

        // 8-bit level -> byte to send (0x80 | 7-bit level) at tableBrightness; and the 7-bit -> limited byte table.
        static readonly byte[] Levels = new byte[256];
        static readonly byte[] Limited = new byte[256];
        static int tableBrightness = -1;

        static byte[] MakeGamma() {
            var table = new byte[256];
            for (var i = 0; i < 256; i++) table[i] = (byte)((i * i * 127 + 255 * 255 / 2) / (255 * 255));
            return table;
        }

        public static int LatchBytes(int count) => (count + 31) / 32;

        public static int BufferSize(int count) => LatchBytes(count) * 2 + count * 3;

        /// <summary>
        /// Fills buffer (BufferSize(count) bytes) for frame. Returns the estimated current in mA after
        /// limiting; wantedMilliamps is what the frame would have drawn without the limit.
        /// </summary>
        public static int Encode(int[] frame, int count, int brightness, int order, int limitMilliamps, byte[] buffer, out int wantedMilliamps) {
            var latch = LatchBytes(count);
            var end = latch + count * 3;
            for (var i = 0; i < latch; i++) {
                buffer[i] = 0;
                buffer[end + i] = 0;
            }

            var levels = Levels;
            if (brightness != tableBrightness) {
                var scale = brightness * 256 / 100;   // brightness 0-100 as a fraction of 256
                for (var v = 0; v < 256; v++) levels[v] = (byte)(0x80 | Gamma[(v * scale) >> 8]);
                tableBrightness = brightness;
            }

            // Where each of the three bytes comes from in 0xRRGGBB.
            var names = LightSettings.Orders[order];
            var s0 = Shift(names[0]);
            var s1 = Shift(names[1]);
            var s2 = Shift(names[2]);

            var sum = 0;
            var p = latch;
            for (var i = 0; i < count; i++) {
                var rgb = frame[i];
                var b0 = levels[(rgb >> s0) & 0xFF];
                var b1 = levels[(rgb >> s1) & 0xFF];
                var b2 = levels[(rgb >> s2) & 0xFF];
                buffer[p] = b0;
                buffer[p + 1] = b1;
                buffer[p + 2] = b2;
                p += 3;
                sum += b0 + b1 + b2;
            }
            sum -= count * 3 * 0x80;   // the 7-bit levels alone

            wantedMilliamps = (int)((long)sum * ChannelMilliamps / 127);
            if (wantedMilliamps <= limitMilliamps || wantedMilliamps == 0) return wantedMilliamps;

            // Too much for the supply: dim every LED by the same factor (one table read per byte).
            var limited = Limited;
            for (var v = 0; v < 128; v++) limited[0x80 | v] = (byte)(0x80 | v * limitMilliamps / wantedMilliamps);
            sum = 0;
            for (var i = latch; i < end; i++) {
                var v = limited[buffer[i]];
                buffer[i] = v;
                sum += v;
            }
            sum -= count * 3 * 0x80;
            return (int)((long)sum * ChannelMilliamps / 127);
        }

        static int Shift(char name) => name == 'R' ? 16 : name == 'G' ? 8 : 0;
    }
}
