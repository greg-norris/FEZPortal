using System;
using System.Drawing;
using PortalDesktop.Data;

namespace PortalDesktop.Ui {
    /// <summary>Icons drawn from shapes, so they scale and need no image resources.</summary>
    public static class Icons {
        // -- Status bar --------------------------------------------------------

        /// <summary>Wi-Fi fan, 20x16 with its top-left at (x, y). bars = 0..3 arcs lit.</summary>
        public static void Wifi(Canvas c, int x, int y, int bars, Color on, Color off) {
            var cx = x + 10;
            var cy = y + 15;
            c.FillArc(bars >= 3 ? on : off, cx, cy, 14, 11, 315, 90);
            c.FillArc(bars >= 2 ? on : off, cx, cy, 9, 6, 315, 90);
            c.FillCircle(bars >= 1 ? on : off, cx, cy - 1, 2);
        }

        /// <summary>SD card outline, 12x15 with its top-left at (x, y).</summary>
        public static void SdCard(Canvas c, int x, int y, Color color, Color bar) {
            c.FillPolygon(color, new[] { x, x + 8, x + 12, x + 12, x }, new[] { y, y, y + 4, y + 15, y + 15 });
            for (var i = 0; i < 3; i++) c.FillRect(bar, x + 3 + i * 3, y + 2, 1, 4);
        }

        /// <summary>"&lt;" chevron, 10 wide, centred vertically on cy.</summary>
        public static void Back(Canvas c, int x, int cy, Color color) {
            c.ThickLine(color, x + 8, cy - 7, x + 1, cy, 3);
            c.ThickLine(color, x + 1, cy, x + 8, cy + 7, 3);
        }

        public static void Forward(Canvas c, int x, int cy, Color color) {
            c.ThickLine(color, x + 1, cy - 7, x + 8, cy, 3);
            c.ThickLine(color, x + 8, cy, x + 1, cy + 7, 3);
        }

        // -- App icons (fit in a circle of radius r around cx, cy) -------------

        public static void ClockFace(Canvas c, int cx, int cy, int r, DateTime time) {
            c.FillCircle(Theme.Accent, cx, cy, r);
            c.FillCircle(Theme.White, cx, cy, r - 3);
            for (var h = 0; h < 12; h += 3) {
                c.FillCircle(Theme.Panel, Canvas.PolarX(cx, r - 7, h * 30), Canvas.PolarY(cy, r - 7, h * 30), 1);
            }
            var minuteAngle = time.Minute * 6;
            var hourAngle = (time.Hour % 12) * 30 + time.Minute / 2;
            c.ThickLine(Theme.Panel, cx, cy, Canvas.PolarX(cx, r * 0.5, hourAngle), Canvas.PolarY(cy, r * 0.5, hourAngle), 4);
            c.ThickLine(Theme.Panel, cx, cy, Canvas.PolarX(cx, r * 0.72, minuteAngle), Canvas.PolarY(cy, r * 0.72, minuteAngle), 3);
            c.FillCircle(Theme.Accent, cx, cy, 3);
        }

        public static void CalendarPage(Canvas c, int cx, int cy, int r, DateTime day) {
            var w = r * 2 - 4;
            var h = r * 2 - 6;
            var x = cx - w / 2;
            var y = cy - h / 2 + 2;
            c.FillRoundRect(Theme.Accent, x, y, w, h, 6);
            c.FillRoundRect(Theme.White, x, y + 12, w, h - 12, 6);
            c.FillRect(Theme.White, x, y + 12, w, 6);
            c.DrawTextCentered(Format.ShortMonth(day.Month).ToUpper(), UiFont.Tiny, Theme.Panel, cx, y);
            // Binder rings
            c.FillPill(Theme.Panel, x + w / 4 - 2, y - 4, 4, 9);
            c.FillPill(Theme.Panel, x + w * 3 / 4 - 2, y - 4, 4, 9);
            c.DrawTextCentered(day.Day.ToString(), UiFont.XLarge, Theme.Panel, cx, y + 15);
        }

        // The GHI Electronics logo as a 22x22 pixel map: b = blue, o = orange, g = grey, . = clear.
        // Made by shrinking the full-size logo; grey is lighter than the logo's so it shows on the dark status bar.
        public const int LogoSize = 22;
        static readonly string[] LogoRows = {
            "......bbbb..gggg......",
            "......bbbb..gggg......",
            "......bbbb..gggg......",
            "......bbbb..gggg......",
            ".....b.....g....g.....",
            "....b.....g......g....",
            "bbbbb.gggg..oooo..gggg",
            "bbbb..gggg..oooo..gggg",
            "bbbb..gggg..oooo..gggg",
            "bbbbb.ggggg.oooo..gggg",
            "....b.....g.....o.....",
            ".....bb....g.....o....",
            "gggg..bbbb..gggg..oooo",
            "gggg..bbbb..gggg..oooo",
            "gggg..bbbb..gggg..oooo",
            "ggggg.bbbb..gggg..oooo",
            "....g......g.....o....",
            ".....gg...g.....o.....",
            "......gggg..oooo......",
            "......gggg..oooo......",
            "......gggg..oooo......",
            "......gggg..oooo......",
        };
        static readonly Color LogoBlue = Color.FromArgb(9, 119, 170);
        static readonly Color LogoOrange = Color.FromArgb(255, 170, 85);
        static readonly Color LogoGrey = Color.FromArgb(150, 150, 150);

        /// <summary>The GHI Electronics logo, LogoSize pixels square, top-left at (x, y).</summary>
        public static void Logo(Canvas c, int x, int y) {
            for (var row = 0; row < LogoRows.Length; row++) {
                var line = LogoRows[row];
                // One FillRect per run of the same colour.
                var start = 0;
                for (var i = 1; i <= line.Length; i++) {
                    if (i < line.Length && line[i] == line[start]) continue;
                    var ch = line[start];
                    if (ch != '.') c.FillRect(ch == 'b' ? LogoBlue : ch == 'o' ? LogoOrange : LogoGrey, x + start, y + row, i - start, 1);
                    start = i;
                }
            }
        }

        /// <summary>Hourglass about 2r tall: white glass, yellow sand.</summary>
        public static void Hourglass(Canvas c, int cx, int cy, int r) {
            var w = r * 3 / 4;
            c.FillTriangle(Theme.White, cx - w, cy - r + 3, cx + w, cy - r + 3, cx, cy + 1);
            c.FillTriangle(Theme.White, cx - w, cy + r - 3, cx + w, cy + r - 3, cx, cy - 1);
            c.FillTriangle(Theme.Accent, cx - w / 2, cy - r / 2 + 2, cx + w / 2, cy - r / 2 + 2, cx, cy - 2);
            c.FillTriangle(Theme.Accent, cx - w + 3, cy + r - 4, cx + w - 3, cy + r - 4, cx, cy + r / 3);
            c.FillRoundRect(Theme.AccentSoft, cx - r, cy - r, r * 2, 4, 2);
            c.FillRoundRect(Theme.AccentSoft, cx - r, cy + r - 4, r * 2, 4, 2);
        }

        /// <summary>Stopwatch about 2r across: white case, yellow hand.</summary>
        public static void Stopwatch(Canvas c, int cx, int cy, int r) {
            var fy = cy + 2;
            c.FillRoundRect(Theme.White, cx - 3, cy - r - 2, 6, 5, 2);
            c.ThickLine(Theme.White, cx + r * 7 / 10, fy - r * 7 / 10 - 1, cx + r * 7 / 10 + 3, fy - r * 7 / 10 - 4, 3);
            c.DrawCircle(Theme.White, cx, fy, r - 2, 3);
            c.ThickLine(Theme.Accent, cx, fy, cx + r / 3, fy - r / 2, 3);
            c.FillCircle(Theme.Accent, cx, fy, 2);
        }

        /// <summary>Alarm bell about 2r tall, in the given colour.</summary>
        public static void Bell(Canvas c, int cx, int cy, int r, Color color) {
            var top = cy - r * 2 / 3;
            c.FillCircle(color, cx, top - r / 4, 2);
            c.FillCircle(color, cx, top + r / 3, r * 3 / 5);
            c.FillPolygon(color,
                new[] { cx - r * 3 / 5, cx + r * 3 / 5, cx + r * 9 / 10, cx - r * 9 / 10 },
                new[] { top + r / 3, top + r / 3, cy + r / 2, cy + r / 2 });
            c.FillRoundRect(color, cx - r, cy + r / 2 - 1, r * 2, 4, 2);
            c.FillCircle(color, cx, cy + r * 4 / 5, r / 5 + 1);
        }

        /// <summary>A calculator about 2r tall: white body, dark display, keys with a yellow "=".</summary>
        public static void Calculator(Canvas c, int cx, int cy, int r) {
            var w = r * 3 / 2;
            var x = cx - w / 2;
            var y = cy - r;
            c.FillRoundRect(Theme.White, x, y, w, r * 2, 5);
            c.FillRoundRect(Theme.PanelDark, x + 4, y + 4, w - 8, r * 2 / 5, 2);
            c.FillRect(Theme.AccentSoft, x + w - 14, y + 4 + r / 5 - 1, 8, 3);
            // 3 x 3 keys, the bottom-right one yellow.
            var keysTop = y + 4 + r * 2 / 5 + 4;
            var kw = (w - 8 - 2 * 3) / 3;
            var kh = (y + r * 2 - 4 - keysTop - 2 * 3) / 3;
            for (var row = 0; row < 3; row++) {
                for (var col = 0; col < 3; col++) {
                    var color = row == 2 && col == 2 ? Theme.Accent : Theme.PanelLight;
                    c.FillRoundRect(color, x + 4 + col * (kw + 3), keysTop + row * (kh + 3), kw, kh, 2);
                }
            }
        }

        public static void StockChart(Canvas c, int cx, int cy, int r) {
            var left = cx - r + 2;
            var bottom = cy + r - 4;
            var barW = r / 3;
            for (var i = 0; i < 4; i++) {
                var bh = r / 2 + i * r / 4;
                c.FillRoundRect(Theme.Track, left + i * (barW + 3), bottom - bh, barW, bh, 2);
            }
            var xs = new[] { left, left + r * 2 / 3, left + r, left + r * 2 - 10 };
            var ys = new[] { cy + r / 3, cy - r / 6, cy + r / 8, cy - r * 2 / 3 };
            for (var i = 0; i < 3; i++) c.ThickLine(Theme.Accent, xs[i], ys[i], xs[i + 1], ys[i + 1], 4);
            var tx = xs[3];
            var ty = ys[3];
            c.FillTriangle(Theme.Accent, tx - 6, ty - 4, tx + 6, ty - 8, tx + 3, ty + 5);
        }

        /// <summary>A folded newspaper about 2r wide: white front page, yellow masthead, a picture and lines of text.</summary>
        public static void Newspaper(Canvas c, int cx, int cy, int r) {
            var w = r * 2 - 6;
            var h = r * 3 / 2 + 2;
            var x = cx - w / 2 + 3;
            var y = cy - h / 2;
            c.FillRoundRect(Theme.CloudDark, x - 8, y + 7, 14, h - 7, 4);      // the inner pages, folded out
            c.FillRoundRect(Theme.White, x, y, w, h, 4);
            var inner = w - 10;
            c.FillRoundRect(Theme.Accent, x + 5, y + 5, inner, h / 6 + 2, 2);   // masthead
            var top = y + 5 + h / 6 + 2 + 4;
            var bottom = y + h - 5;
            var pw = inner * 2 / 5;
            c.FillRoundRect(Theme.AccentSoft, x + 5, top, pw, bottom - top, 2);   // picture
            var lx = x + 5 + pw + 4;
            var lw = inner - pw - 4;
            for (var ly = top; ly + 3 <= bottom; ly += 5) c.FillRect(Theme.Track, lx, ly, lw, 3);
        }

        // -- Weather -----------------------------------------------------------

        /// <summary>
        /// Weather icon filling roughly cx±s, cy±s. bg is the colour behind the icon
        /// (the moon's shadow is drawn with it).
        /// </summary>
        public static void Weather(Canvas c, WeatherKind kind, bool isDay, int cx, int cy, int s, Color bg) {
            switch (kind) {
                case WeatherKind.Clear:
                    if (isDay) Sun(c, cx, cy, s * 11 / 20, s);
                    else Moon(c, cx, cy, s * 3 / 4, bg);
                    break;

                case WeatherKind.PartlyCloudy:
                    if (isDay) Sun(c, cx - s * 3 / 10, cy - s * 3 / 10, s * 2 / 5, s * 7 / 10);
                    else Moon(c, cx - s * 3 / 10, cy - s * 3 / 10, s / 2, bg);
                    Cloud(c, cx + s / 8, cy + s / 4, s * 3 / 2, Theme.Cloud);
                    break;

                case WeatherKind.Cloudy:
                    Cloud(c, cx + s / 4, cy - s / 4, s * 6 / 5, Theme.CloudDark);
                    Cloud(c, cx - s / 8, cy + s / 5, s * 3 / 2, Theme.Cloud);
                    break;

                case WeatherKind.Fog:
                    Cloud(c, cx, cy - s / 3, s * 3 / 2, Theme.CloudDark);
                    var t = Math.Max(3, s / 6);
                    c.FillPill(Theme.Cloud, cx - s * 4 / 5, cy + s / 4, s * 8 / 5, t);
                    c.FillPill(Theme.Cloud, cx - s * 3 / 5, cy + s / 4 + t * 2, s * 6 / 5, t);
                    break;

                case WeatherKind.Drizzle:
                case WeatherKind.Rain:
                case WeatherKind.Storm:
                case WeatherKind.Snow:
                    Cloud(c, cx, cy - s / 4, s * 8 / 5, kind == WeatherKind.Storm ? Theme.CloudDark : Theme.Cloud);
                    var top = cy + s / 3;
                    if (kind == WeatherKind.Storm) {
                        var bx = cx - s / 10;
                        c.FillPolygon(Theme.Accent,
                            new[] { bx + s / 5, bx - s / 6, bx + s / 20, bx - s / 8, bx + s / 3, bx + s / 8 },
                            new[] { top - s / 6, top + s * 2 / 5, top + s * 2 / 5, top + s * 4 / 5, top + s / 5, top + s / 5 });
                    }
                    else if (kind == WeatherKind.Snow) {
                        var fr = Math.Max(2, s / 9);
                        for (var i = -1; i <= 1; i++) {
                            c.FillCircle(Theme.White, cx + i * s / 2, top + s / 6 + (i == 0 ? s / 4 : 0), fr);
                        }
                    }
                    else {
                        var drops = kind == WeatherKind.Drizzle ? 2 : 3;
                        var len = kind == WeatherKind.Drizzle ? s / 4 : s * 2 / 5;
                        var th = Math.Max(2, s / 8);
                        for (var i = 0; i < drops; i++) {
                            var dx = cx + (i - (drops - 1) / 2.0) * s / 2;
                            c.ThickLine(Theme.Rain, (int)dx + s / 8, top + s / 10, (int)dx - s / 16, top + s / 10 + len, th);
                        }
                    }
                    break;
            }
        }

        static void Sun(Canvas c, int cx, int cy, int r, int rayEnd) {
            var th = Math.Max(2, r / 4);
            for (var a = 0; a < 360; a += 45) {
                c.ThickLine(Theme.Accent,
                    Canvas.PolarX(cx, r + th + 1, a), Canvas.PolarY(cy, r + th + 1, a),
                    Canvas.PolarX(cx, rayEnd, a), Canvas.PolarY(cy, rayEnd, a), th);
            }
            c.FillCircle(Theme.Accent, cx, cy, r);
        }

        static void Moon(Canvas c, int cx, int cy, int r, Color bg) {
            c.FillCircle(Theme.Cloud, cx, cy, r);
            c.FillCircle(bg, cx + r * 9 / 20, cy - r * 7 / 20, r * 4 / 5);
        }

        /// <summary>A cloud about w wide, sitting on cy.</summary>
        static void Cloud(Canvas c, int cx, int cy, int w, Color color) {
            var h = w * 2 / 5;
            c.FillCircle(color, cx - w / 6, cy - h / 2, w / 5);
            c.FillCircle(color, cx + w / 10, cy - h * 3 / 5, w * 27 / 100);
            c.FillPill(color, cx - w / 2, cy - h / 2, w, h);
        }
    }
}
