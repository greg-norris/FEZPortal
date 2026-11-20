using System;
using System.Drawing;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// A 7-segment LED clock: HH:MM:SS in one row with blinking colons, and AM/PM with an alarm
    /// bell under the right end, on a dark panel where unlit segments glow faintly like a real
    /// display. Sized to any box.
    /// </summary>
    public static class SevenSegment {
        // Clock faces (Config.ClockFace).
        public const int Analog = 0, Red = 1, Green = 2, Blue = 3, FaceCount = 4;

        static readonly Color Panel = Color.FromArgb(8, 10, 14);
        static readonly Color[] Colors = {
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(255, 45, 45),
            Color.FromArgb(50, 255, 100),
            Color.FromArgb(60, 160, 255),
        };

        // Segments a..g as bits 0..6, for digits 0..9.
        const int A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64;
        static readonly int[] Digits = {
            A | B | C | D | E | F, B | C, A | B | G | E | D, A | B | G | C | D, F | G | B | C,
            A | F | G | C | D, A | F | G | E | D | C, A | B | C, A | B | C | D | E | F | G, A | B | C | D | F | G,
        };

        public static string FaceName(int face) => face == Red ? "Red" : face == Green ? "Green" : face == Blue ? "Blue" : "Analog";

        /// <summary>
        /// Draws the clock in the box. With secondsOnly, only the seconds and the colons are redrawn
        /// (enough for a tick that doesn't change the minute).
        /// </summary>
        public static void Draw(Canvas c, int x, int y, int w, int h, DateTime now, bool use24, int face, bool alarmSet, bool secondsOnly) {
            var color = Colors[face > 0 && face < FaceCount ? face : Red];
            var dim = Canvas.Lerp(Panel, color, 0.12);

            // HH:MM:SS in one row, as large as the box allows: the row is about 4.75 digit heights
            // wide (6 digits, 2 colons and the gaps); AM/PM goes in a small line underneath.
            var dh = Math.Min(w * 90 / 100 * 100 / 475, h * 80 / 100);
            var dw = dh * 56 / 100;
            var t = Math.Max(3, dh * 13 / 100);
            var gap = dw * 24 / 100;
            var colonW = t * 26 / 10;
            var rowW = 6 * dw + 7 * gap + 2 * colonW;   // gaps: inside each pair (3) and on both sides of each colon (4)
            var font = dh >= 70 ? UiFont.XLarge : UiFont.Medium;
            var labelH = use24 && !alarmSet ? 0 : c.LineHeight(font) + dh / 10;
            // Nearly against the left edge (1/12 of the spare width): a leading 1 or 0 lights only the
            // right of its digit, so a centred row looks shifted right.
            var x0 = x + (w - rowW) / 12;
            var y0 = y + (h - dh - labelH) / 2;

            // Left to right: H H : M M : S S
            var pair = 2 * dw + gap;
            var colon1 = x0 + pair + gap;
            var minutesX = colon1 + colonW + gap;
            var colon2 = minutesX + pair + gap;
            var secondsX = colon2 + colonW + gap;

            var hour = use24 ? now.Hour : (now.Hour + 11) % 12 + 1;
            var h1 = use24 || hour >= 10 ? Digits[hour / 10] : 0;   // 12-hour: no leading zero (segments stay dim)

            if (!secondsOnly) {
                c.FillRoundRect(Panel, x, y, w, h, 14);
                DigitAt(c, x0, y0, dw, dh, t, h1, color, dim);
                DigitAt(c, x0 + dw + gap, y0, dw, dh, t, Digits[hour % 10], color, dim);
                DigitAt(c, minutesX, y0, dw, dh, t, Digits[now.Minute / 10], color, dim);
                DigitAt(c, minutesX + dw + gap, y0, dw, dh, t, Digits[now.Minute % 10], color, dim);
                // AM/PM under the right end, with the alarm bell just left of it (or in its place on the 24-hour clock).
                var labelY = y0 + dh + dh / 10;
                var labelRight = secondsX + pair;
                if (!use24) {
                    var ampm = now.Hour < 12 ? "AM" : "PM";
                    c.DrawTextRight(ampm, font, color, labelRight, labelY);
                    labelRight -= c.MeasureText(ampm, font) + c.LineHeight(font) / 3;
                }
                if (alarmSet) {
                    var r = c.LineHeight(font) * 4 / 10;
                    Icons.Bell(c, labelRight - r, labelY + c.LineHeight(font) / 2, r, Theme.Accent);
                }
            }
            else {
                c.FillRect(Panel, secondsX - 2, y0 - 2, pair + 4, dh + 4);
                c.FillRect(Panel, colon1, y0, colonW, dh);
                c.FillRect(Panel, colon2, y0, colonW, dh);
            }

            // Colons: on for even seconds.
            var colonColor = now.Second % 2 == 0 ? color : dim;
            Colon(c, colon1, y0, colonW, dh, t, colonColor);
            Colon(c, colon2, y0, colonW, dh, t, colonColor);

            DigitAt(c, secondsX, y0, dw, dh, t, Digits[now.Second / 10], color, dim);
            DigitAt(c, secondsX + dw + gap, y0, dw, dh, t, Digits[now.Second % 10], color, dim);
        }

        static void Colon(Canvas c, int x, int y, int w, int h, int t, Color color) {
            var dot = Math.Max(3, t * 11 / 10);
            var cx = x + (w - dot) / 2;
            c.FillRect(color, cx, y + h * 30 / 100 - dot / 2, dot, dot);
            c.FillRect(color, cx, y + h * 70 / 100 - dot / 2, dot, dot);
        }

        // -- One digit ----------------------------------------------------------------

        // Reused point arrays for the six-sided segments (drawing is on the UI thread only).
        static readonly int[] Xs = new int[6], Ys = new int[6];

        static void DigitAt(Canvas c, int x, int y, int w, int h, int t, int mask, Color on, Color off) {
            var half = t / 2;
            var left = x + half;
            var right = x + w - half;
            var top = y + half;
            var mid = y + h / 2;
            var bottom = y + h - half;
            var g = Math.Max(1, t / 4);   // gap between segments
            Horizontal(c, (mask & A) != 0 ? on : off, left + g, right - g, top, t);
            Horizontal(c, (mask & G) != 0 ? on : off, left + g, right - g, mid, t);
            Horizontal(c, (mask & D) != 0 ? on : off, left + g, right - g, bottom, t);
            Vertical(c, (mask & F) != 0 ? on : off, left, top + g, mid - g, t);
            Vertical(c, (mask & B) != 0 ? on : off, right, top + g, mid - g, t);
            Vertical(c, (mask & E) != 0 ? on : off, left, mid + g, bottom - g, t);
            Vertical(c, (mask & C) != 0 ? on : off, right, mid + g, bottom - g, t);
        }

        /// <summary>A horizontal segment with pointed ends, centred on y.</summary>
        static void Horizontal(Canvas c, Color color, int x0, int x1, int y, int t) {
            var h = t / 2;
            Xs[0] = x0; Ys[0] = y;
            Xs[1] = x0 + h; Ys[1] = y - h;
            Xs[2] = x1 - h; Ys[2] = y - h;
            Xs[3] = x1; Ys[3] = y;
            Xs[4] = x1 - h; Ys[4] = y + h + 1;
            Xs[5] = x0 + h; Ys[5] = y + h + 1;
            c.FillPolygon(color, Xs, Ys);
        }

        /// <summary>A vertical segment with pointed ends, centred on x.</summary>
        static void Vertical(Canvas c, Color color, int x, int y0, int y1, int t) {
            var h = t / 2;
            Xs[0] = x; Ys[0] = y0;
            Xs[1] = x + h + 1; Ys[1] = y0 + h;
            Xs[2] = x + h + 1; Ys[2] = y1 - h;
            Xs[3] = x; Ys[3] = y1;
            Xs[4] = x - h; Ys[4] = y1 - h;
            Xs[5] = x - h; Ys[5] = y0 + h;
            c.FillPolygon(color, Xs, Ys);
        }
    }
}
