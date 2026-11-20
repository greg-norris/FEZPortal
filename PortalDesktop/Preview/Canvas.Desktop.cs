using System;
using System.Drawing;
using System.IO;

namespace PortalDesktop.Ui {
    /// <summary>
    /// Desktop stand-in for the device primitives. Draws into a 480x272 bitmap and renders text
    /// from the same .tcfnt files the device uses, so text metrics match the hardware.
    /// </summary>
    public partial class Canvas {
        // Resource file names, in the same order as the UiFont enum.
        static readonly string[] FontFiles = { "FontTiny", "FontSmall", "FontMedium", "FontLarge", "FontXLarge", "FontHuge", "FontClock", "FontPill" };

        readonly Bitmap bitmap;
        readonly Graphics g;
        readonly TcFont[] fonts;

        public Canvas(int width, int height, string fontDir) {
            this.bitmap = new Bitmap(width, height);
            this.g = Graphics.FromImage(this.bitmap);
            this.fonts = new TcFont[FontFiles.Length];
            for (var i = 0; i < FontFiles.Length; i++) {
                this.fonts[i] = new TcFont(Path.Combine(fontDir, FontFiles[i] + ".tcfnt"));
            }
        }

        public int Width => this.bitmap.Width;
        public int Height => this.bitmap.Height;
        public Bitmap Bitmap => this.bitmap;

        public void FillRect(Color color, int x, int y, int w, int h) {
            if (w <= 0 || h <= 0) return;
            using (var b = new SolidBrush(color)) this.g.FillRectangle(b, x, y, w, h);
        }

        public void DrawText(string text, UiFont font, Color color, int x, int y) =>
            this.fonts[(int)font].Draw(this.bitmap, text, color, x, y);

        public int MeasureText(string text, UiFont font) => this.fonts[(int)font].Measure(text);

        public int LineHeight(UiFont font) => this.fonts[(int)font].Height;

        public void Flush() { }

        public void Flush(int x, int y, int w, int h) { }
    }

    /// <summary>Minimal .tcfnt reader (TinyCLR resource header + NETMF tinyfnt).</summary>
    sealed class TcFont {
        struct Range { public int FirstIndex, First, Last, Offset; }

        readonly Range[] ranges;
        readonly int[] charOffset, marginLeft, marginRight;
        readonly byte[] bits;
        readonly int stride, bitmapHeight, yOffset;

        public int Height { get; }

        public TcFont(string path) {
            var d = File.ReadAllBytes(path);
            var headerSize = BitConverter.ToInt32(d, 8);
            var resHeaderSize = BitConverter.ToInt32(d, 12);
            var count = BitConverter.ToInt32(d, 16);
            var p = headerSize + resHeaderSize * count;

            this.Height = BitConverter.ToUInt16(d, p);
            this.yOffset = BitConverter.ToInt16(d, p + 2);
            var nRanges = BitConverter.ToUInt16(d, p + 16);
            var nChars = BitConverter.ToUInt16(d, p + 18);
            var bitmapWidth = BitConverter.ToInt32(d, p + 24);
            this.bitmapHeight = BitConverter.ToInt32(d, p + 28);
            p += 36;

            this.ranges = new Range[nRanges + 1];
            for (var i = 0; i <= nRanges; i++, p += 12) {
                this.ranges[i] = new Range {
                    FirstIndex = BitConverter.ToInt32(d, p),
                    First = BitConverter.ToUInt16(d, p + 4),
                    Last = BitConverter.ToUInt16(d, p + 6),
                    Offset = BitConverter.ToInt32(d, p + 8),
                };
            }
            this.charOffset = new int[nChars + 1];
            this.marginLeft = new int[nChars + 1];
            this.marginRight = new int[nChars + 1];
            for (var i = 0; i <= nChars; i++, p += 4) {
                this.charOffset[i] = BitConverter.ToUInt16(d, p);
                this.marginLeft[i] = (sbyte)d[p + 2];
                this.marginRight[i] = (sbyte)d[p + 3];
            }
            this.stride = (bitmapWidth + 31) / 32 * 4;
            this.bits = new byte[this.stride * this.bitmapHeight];
            Array.Copy(d, p, this.bits, 0, this.bits.Length);
        }

        bool Glyph(char ch, out int x, out int width, out int left, out int right) {
            for (var r = 0; r < this.ranges.Length - 1; r++) {
                var range = this.ranges[r];
                if (ch < range.First || ch > range.Last) continue;
                var i = range.FirstIndex + ch - range.First;
                x = range.Offset + this.charOffset[i];
                width = ch < range.Last ? this.charOffset[i + 1] - this.charOffset[i] : this.ranges[r + 1].Offset - x;
                left = this.marginLeft[i];
                right = this.marginRight[i];
                return true;
            }
            x = width = left = right = 0;
            return ch != '�' && this.Glyph('�', out x, out width, out left, out right);
        }

        public int Measure(string text) {
            var total = 0;
            foreach (var ch in text) {
                if (this.Glyph(ch, out _, out var w, out var l, out var r)) total += l + w + r;
            }
            return total;
        }

        public void Draw(Bitmap target, string text, Color color, int x, int y) {
            foreach (var ch in text) {
                if (!this.Glyph(ch, out var gx, out var w, out var l, out var r)) continue;
                x += l;
                for (var row = 0; row < this.bitmapHeight; row++) {
                    for (var col = 0; col < w; col++) {
                        var bit = gx + col;
                        if ((this.bits[row * this.stride + bit / 8] >> (bit % 8) & 1) == 0) continue;
                        var px = x + col;
                        var py = y + this.yOffset + row;
                        if (px >= 0 && py >= 0 && px < target.Width && py < target.Height) target.SetPixel(px, py, color);
                    }
                }
                x += w + r;
            }
        }
    }
}
