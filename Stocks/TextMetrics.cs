using System.Collections.Concurrent;
using System.Text;

namespace Stocks;

/// <summary>
/// Text widths read straight from the TrueType file (cmap + hmtx), scaled
/// the way LVGL's tiny_ttf renderer scales them. The UI library can draw text
/// but cannot measure it, and the browser needs widths to cut tab titles and
/// URLs with "…" and to know how tall a wrapped paragraph will be.
/// </summary>
sealed class TextMetrics {
    private readonly byte[] ttf;
    private readonly int unitsPerEm;
    private readonly int ascender, descender, lineGap;
    private readonly ushort[] advances;
    private readonly int cmap4;
    private readonly ConcurrentDictionary<char, int> advanceCache = new();   // pages are laid out off the UI thread

    public TextMetrics(byte[] ttf) {
        this.ttf = ttf;
        int head = 0, hhea = 0, hmtx = 0, cmap = 0;
        int numTables = this.U16(4);
        for (var i = 0; i < numTables; i++) {
            var record = 12 + 16 * i;
            var offset = (int)this.U32(record + 8);
            switch (Encoding.ASCII.GetString(ttf, record, 4)) {
                case "head": head = offset; break;
                case "hhea": hhea = offset; break;
                case "hmtx": hmtx = offset; break;
                case "cmap": cmap = offset; break;
            }
        }
        if (head == 0 || hhea == 0 || hmtx == 0 || cmap == 0) throw new InvalidDataException("Not a TrueType font");

        this.unitsPerEm = this.U16(head + 18);
        this.ascender = this.S16(hhea + 4);
        this.descender = this.S16(hhea + 6);
        this.lineGap = this.S16(hhea + 8);
        int numberOfHMetrics = this.U16(hhea + 34);
        this.advances = new ushort[numberOfHMetrics];
        for (var i = 0; i < numberOfHMetrics; i++) this.advances[i] = this.U16(hmtx + 4 * i);

        // Unicode BMP character map (format 4).
        int subtables = this.U16(cmap + 2);
        for (var i = 0; i < subtables && this.cmap4 == 0; i++) {
            var record = cmap + 4 + 8 * i;
            int platform = this.U16(record), encoding = this.U16(record + 2);
            var sub = cmap + (int)this.U32(record + 4);
            if (((platform == 3 && encoding == 1) || platform == 0) && this.U16(sub) == 4) this.cmap4 = sub;
        }
        if (this.cmap4 == 0) throw new InvalidDataException("Font has no Unicode BMP cmap");
    }

    private double Scale(int sizePx) => (double)sizePx / this.unitsPerEm;

    /// <summary>Height of one line of text, as LVGL spaces label lines.</summary>
    public int LineHeight(int sizePx) =>
        (int)(this.Scale(sizePx) * (this.ascender - this.descender + this.lineGap));

    public int Advance(char c, int sizePx) =>
        (int)Math.Floor(this.AdvanceUnits(c) * this.Scale(sizePx) + 0.5);

    private int AdvanceUnits(char c) {
        if (!this.advanceCache.TryGetValue(c, out var units)) {
            var glyph = this.Glyph(c);
            units = this.advances[Math.Min(glyph, this.advances.Length - 1)];
            this.advanceCache[c] = units;
        }
        return units;
    }

    // -- Canvas text ---------------------------------------------------------
    // Canvas.DrawText sizes text by pixel height: ascent to descent equals
    // sizePx (stb_truetype's ScaleForPixelHeight), unlike LVGL's labels,
    // which size by the em square.

    private double CanvasScale(int sizePx) => (double)sizePx / (this.ascender - this.descender);

    /// <summary>
    /// Width of <paramref name="text"/> drawn with Canvas.DrawText at <paramref name="sizePx"/>.
    /// The renderer rounds each glyph's advance down (measured on the board).
    /// </summary>
    public int MeasureCanvas(string text, int sizePx) {
        var scale = this.CanvasScale(sizePx);
        var width = 0;
        foreach (var c in text) width += (int)(this.AdvanceUnits(c) * scale);
        return width;
    }

    public int MeasureCanvas(char c, int sizePx) => (int)(this.AdvanceUnits(c) * this.CanvasScale(sizePx));

    /// <summary>Baseline offset from the top of Canvas.DrawText's text box.</summary>
    public int CanvasAscent(int sizePx) => (int)Math.Round(this.ascender * this.CanvasScale(sizePx));

    public int Measure(string text, int sizePx) {
        var width = 0;
        foreach (var c in text) width += this.Advance(c, sizePx);
        return width;
    }

    /// <summary>Returns <paramref name="text"/>, cut and ended with "…" if wider than <paramref name="maxWidth"/>.</summary>
    public string Ellipsize(string text, int sizePx, int maxWidth) {
        if (this.Measure(text, sizePx) <= maxWidth) return text;
        var budget = maxWidth - this.Advance('…', sizePx);
        var width = 0;
        var end = 0;
        while (end < text.Length && width + this.Advance(text[end], sizePx) <= budget) width += this.Advance(text[end++], sizePx);
        return text[..end].TrimEnd() + "…";
    }

    /// <summary>Greedy word wrap into lines no wider than <paramref name="maxWidth"/>.</summary>
    public List<string> Wrap(string text, int sizePx, int maxWidth) {
        var lines = new List<string>();
        var space = this.Advance(' ', sizePx);
        foreach (var paragraph in text.Split('\n')) {
            var line = new StringBuilder();
            var lineWidth = 0;
            foreach (var word in paragraph.Split(' ')) {
                var wordWidth = this.Measure(word, sizePx);
                if (line.Length > 0 && lineWidth + space + wordWidth <= maxWidth) {
                    line.Append(' ').Append(word);
                    lineWidth += space + wordWidth;
                    continue;
                }
                if (line.Length > 0) {
                    lines.Add(line.ToString());
                    line.Clear();
                    lineWidth = 0;
                }
                // A word longer than the whole line is split by characters.
                foreach (var c in word) {
                    var w = this.Advance(c, sizePx);
                    if (lineWidth + w > maxWidth && line.Length > 0) {
                        lines.Add(line.ToString());
                        line.Clear();
                        lineWidth = 0;
                    }
                    line.Append(c);
                    lineWidth += w;
                }
            }
            lines.Add(line.ToString());
        }
        return lines;
    }

    private int Glyph(char c) {
        int segCount = this.U16(this.cmap4 + 6) / 2;
        var ends = this.cmap4 + 14;
        var starts = ends + 2 * segCount + 2;
        var deltas = starts + 2 * segCount;
        var rangeOffsets = deltas + 2 * segCount;
        for (var i = 0; i < segCount; i++) {
            if (this.U16(ends + 2 * i) < c) continue;
            int start = this.U16(starts + 2 * i);
            if (start > c) return 0;
            int delta = this.S16(deltas + 2 * i);
            int rangeOffsetAt = rangeOffsets + 2 * i;
            int rangeOffset = this.U16(rangeOffsetAt);
            if (rangeOffset == 0) return (c + delta) & 0xFFFF;
            int glyph = this.U16(rangeOffsetAt + rangeOffset + 2 * (c - start));
            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }
        return 0;
    }

    private ushort U16(int at) => (ushort)((this.ttf[at] << 8) | this.ttf[at + 1]);
    private short S16(int at) => (short)this.U16(at);
    private uint U32(int at) => ((uint)this.U16(at) << 16) | this.U16(at + 2);
}
