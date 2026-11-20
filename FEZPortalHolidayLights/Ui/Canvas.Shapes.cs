using System;
using System.Drawing;

namespace HolidayLights.Ui {
    /// <summary>
    /// Shape drawing built only from horizontal FillRect spans, so it renders identically on the
    /// device and in the desktop preview. Angles are in degrees, 0 = 12 o'clock, clockwise.
    /// </summary>
    public partial class Canvas {
        public void FillCircle(Color color, int cx, int cy, int r) {
            for (var dy = -r; dy <= r; dy++) {
                var half = HalfSpan(r, dy);
                this.FillRect(color, cx - half, cy + dy, half * 2 + 1, 1);
            }
        }

        public void FillRoundRect(Color color, int x, int y, int w, int h, int r) {
            if (r * 2 > h) r = h / 2;
            if (r * 2 > w) r = w / 2;
            for (var i = 0; i < r; i++) {
                // Inset for the corner row i rows from the top/bottom edge.
                var inset = r - HalfSpan(r, r - i);
                this.FillRect(color, x + inset, y + i, w - inset * 2, 1);
                this.FillRect(color, x + inset, y + h - 1 - i, w - inset * 2, 1);
            }
            this.FillRect(color, x, y + r, w, h - r * 2);
        }

        public void FillPill(Color color, int x, int y, int w, int h) => this.FillRoundRect(color, x, y, w, h, h / 2);

        /// <summary>Fills the part of a ring between rInner and rOuter that lies inside the given arc.</summary>
        public void FillArc(Color color, int cx, int cy, int rOuter, int rInner, double startDeg, double sweepDeg) {
            var outer2 = (2 * rOuter + 1) * (2 * rOuter + 1);
            var inner2 = rInner > 0 ? (2 * rInner - 1) * (2 * rInner - 1) : -1;
            var full = sweepDeg >= 360;

            for (var dy = -rOuter; dy <= rOuter; dy++) {
                var runStart = 0;
                var inRun = false;
                for (var dx = -rOuter; dx <= rOuter + 1; dx++) {
                    var on = false;
                    if (dx <= rOuter) {
                        var d2 = 4 * (dx * dx + dy * dy);
                        if (d2 <= outer2 && d2 >= inner2) {
                            on = full || InSweep(AngleOf(dx, dy), startDeg, sweepDeg);
                        }
                    }
                    if (on && !inRun) { runStart = dx; inRun = true; }
                    else if (!on && inRun) {
                        this.FillRect(color, cx + runStart, cy + dy, dx - runStart, 1);
                        inRun = false;
                    }
                }
            }
        }

        public void FillRing(Color color, int cx, int cy, int rOuter, int rInner) => this.FillArc(color, cx, cy, rOuter, rInner, 0, 360);

        /// <summary>Draws a line of the given thickness with round ends (a filled quad plus two end circles).</summary>
        public void ThickLine(Color color, int x0, int y0, int x1, int y1, int thickness) {
            var r = thickness / 2;
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (thickness <= 2 || len < 1) {
                this.FillCircle(color, x0, y0, r);
                if (len >= 1) this.Polyline(color, new[] { x0, x1 }, new[] { y0, y1 }, thickness);
                return;
            }
            var nx = -dy / len * thickness / 2.0;
            var ny = dx / len * thickness / 2.0;
            this.FillPolygon(color,
                new[] { (int)Math.Round(x0 + nx), (int)Math.Round(x1 + nx), (int)Math.Round(x1 - nx), (int)Math.Round(x0 - nx) },
                new[] { (int)Math.Round(y0 + ny), (int)Math.Round(y1 + ny), (int)Math.Round(y1 - ny), (int)Math.Round(y0 - ny) });
            this.FillCircle(color, x0, y0, r);
            this.FillCircle(color, x1, y1, r);
        }

        /// <summary>
        /// Connected line segments of the given thickness, drawn as one vertical span per pixel column
        /// (cheap for charts with many points).
        /// </summary>
        public void Polyline(Color color, int[] xs, int[] ys, int thickness) => this.Polyline(color, xs, ys, xs.Length, thickness);

        /// <summary>Polyline through the first count points (for reused point arrays).</summary>
        public void Polyline(Color color, int[] xs, int[] ys, int count, int thickness) {
            for (var i = 0; i + 1 < count; i++) {
                int ax = xs[i], ay = ys[i], bx = xs[i + 1], by = ys[i + 1];
                if (bx < ax) { var t = ax; ax = bx; bx = t; t = ay; ay = by; by = t; }
                if (bx == ax) {
                    this.FillRect(color, ax, Math.Min(ay, by), thickness, Math.Abs(by - ay) + thickness);
                    continue;
                }
                for (var x = ax; x < bx; x++) {
                    var yA = ay + (by - ay) * (x - ax) / (double)(bx - ax);
                    var yB = ay + (by - ay) * (x + 1 - ax) / (double)(bx - ax);
                    var top = (int)Math.Round(Math.Min(yA, yB));
                    var bottom = (int)Math.Round(Math.Max(yA, yB));
                    this.FillRect(color, x, top, thickness, bottom - top + thickness);
                }
            }
        }

        /// <summary>Fills a polygon (even-odd rule) given its vertex coordinates.</summary>
        public void FillPolygon(Color color, int[] xs, int[] ys) {
            var n = xs.Length;
            var minY = ys[0];
            var maxY = ys[0];
            for (var i = 1; i < n; i++) {
                if (ys[i] < minY) minY = ys[i];
                if (ys[i] > maxY) maxY = ys[i];
            }
            var hits = new int[n];
            for (var y = minY; y <= maxY; y++) {
                var count = 0;
                var sy = y + 0.5;
                for (var i = 0; i < n; i++) {
                    var j = (i + 1) % n;
                    var ya = ys[i]; var yb = ys[j];
                    if ((ya <= sy && yb > sy) || (yb <= sy && ya > sy)) {
                        var t = (sy - ya) / (yb - ya);
                        hits[count++] = (int)(xs[i] + t * (xs[j] - xs[i]) + 0.5);
                    }
                }
                // Insertion sort; polygons here have only a handful of edges.
                for (var a = 1; a < count; a++) {
                    var v = hits[a];
                    var b = a - 1;
                    while (b >= 0 && hits[b] > v) { hits[b + 1] = hits[b]; b--; }
                    hits[b + 1] = v;
                }
                for (var k = 0; k + 1 < count; k += 2) {
                    this.FillRect(color, hits[k], y, hits[k + 1] - hits[k], 1);
                }
            }
        }

        public void FillTriangle(Color color, int x0, int y0, int x1, int y1, int x2, int y2) =>
            this.FillPolygon(color, new[] { x0, x1, x2 }, new[] { y0, y1, y2 });

        /// <summary>Fills a rectangle with a vertical gradient from top to bottom.</summary>
        public void FillGradientV(Color top, Color bottom, int x, int y, int w, int h) {
            for (var i = 0; i < h; i++) {
                this.FillRect(Lerp(top, bottom, h > 1 ? (double)i / (h - 1) : 0), x, y + i, w, 1);
            }
        }

        public void DrawTextCentered(string text, UiFont font, Color color, int cx, int y) {
            this.DrawText(text, font, color, cx - this.MeasureText(text, font) / 2, y);
        }

        public void DrawTextRight(string text, UiFont font, Color color, int right, int y) {
            this.DrawText(text, font, color, right - this.MeasureText(text, font), y);
        }

        /// <summary>Shortens text with "..." so it fits in maxWidth pixels.</summary>
        public string Ellipsize(string text, UiFont font, int maxWidth) {
            if (text == null) return "";
            if (this.MeasureText(text, font) <= maxWidth) return text;
            // Longest prefix that fits with "..." (binary search: few strings, as this runs on every redraw).
            var best = "";
            int lo = 1, hi = text.Length - 1;
            while (lo <= hi) {
                var n = (lo + hi) / 2;
                var s = text.Substring(0, n).TrimEnd() + "...";
                if (this.MeasureText(s, font) <= maxWidth) { best = s; lo = n + 1; }
                else hi = n - 1;
            }
            return best;
        }

        /// <summary>
        /// Splits text into lines that fit maxWidth: at line breaks, then between words; a word
        /// too long for a line on its own is split wherever it has to be. Blank lines are kept.
        /// </summary>
        public System.Collections.ArrayList Wrap(string text, UiFont font, int maxWidth) {
            var lines = new System.Collections.ArrayList();
            foreach (var paragraph in text.Split('\n')) {
                var line = "";
                foreach (var word in paragraph.Split(' ')) {
                    if (word.Length == 0) continue;
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (this.MeasureText(candidate, font) <= maxWidth) {
                        line = candidate;
                        continue;
                    }
                    if (line.Length > 0) lines.Add(line);
                    // The word starts a new line; split it if it still doesn't fit.
                    var rest = word;
                    while (this.MeasureText(rest, font) > maxWidth && rest.Length > 1) {
                        var n = rest.Length - 1;
                        while (n > 1 && this.MeasureText(rest.Substring(0, n), font) > maxWidth) n--;
                        lines.Add(rest.Substring(0, n));
                        rest = rest.Substring(n);
                    }
                    line = rest;
                }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>A thin circle outline, drawn span by span (cheap enough to redraw every second).</summary>
        public void DrawCircle(Color color, int cx, int cy, int r, int thickness) {
            var inner = r - thickness;
            for (var dy = -r; dy <= r; dy++) {
                var outerHalf = HalfSpan(r, dy);
                if (inner < 0 || dy < -inner || dy > inner) {
                    this.FillRect(color, cx - outerHalf, cy + dy, outerHalf * 2 + 1, 1);
                    continue;
                }
                var innerHalf = HalfSpan(inner, dy);
                this.FillRect(color, cx - outerHalf, cy + dy, outerHalf - innerHalf, 1);
                this.FillRect(color, cx + innerHalf + 1, cy + dy, outerHalf - innerHalf, 1);
            }
        }

        public static Color Lerp(Color a, Color b, double t) =>
            Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));

        /// <summary>Point on a circle at the given angle (0 = 12 o'clock, clockwise).</summary>
        public static int PolarX(int cx, double r, double deg) => cx + (int)Math.Round(r * Math.Sin(deg * Math.PI / 180));
        public static int PolarY(int cy, double r, double deg) => cy - (int)Math.Round(r * Math.Cos(deg * Math.PI / 180));

        static int HalfSpan(int r, int dy) {
            // Widest |dx| with (dx, dy) inside a circle of radius r + 0.5.
            var v = (r + 0.5) * (r + 0.5) - dy * dy;
            return v <= 0 ? 0 : (int)Math.Sqrt(v);
        }

        static double AngleOf(int dx, int dy) {
            var a = Math.Atan2(dx, -dy) * 180 / Math.PI;
            return a < 0 ? a + 360 : a;
        }

        static bool InSweep(double angle, double start, double sweep) {
            var rel = angle - start;
            while (rel < 0) rel += 360;
            while (rel >= 360) rel -= 360;
            return rel <= sweep;
        }
    }
}
