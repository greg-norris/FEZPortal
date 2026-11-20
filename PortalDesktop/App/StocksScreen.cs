using System;
using System.Drawing;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Watchlist as a grid of cards (8 per page, slide up for more), and a detail view with a
    /// session chart and the day's numbers. Charts are built from the quotes seen while the
    /// Portal runs, laid out over the regular session (9:30-16:00 New York time).
    /// The + in the status bar adds a symbol (typed on the on-screen keyboard); Remove in the status
    /// bar of the detail view takes one off. Both save the list to stocks.txt.
    /// </summary>
    public class StocksScreen : Screen {
        // Watchlist grid: 4 x 2 cards.
        const int Cols = 4, Rows = 2, PerPage = Cols * Rows;
        const int GridX = Theme.Margin, GridY = 38, CardW = 110, CardH = 109, Gap = 8, Pad = 8, CardRadius = 10;

        // Detail view: header and chart on the left, numbers panel on the right.
        const int MainX = 12, MainRight = 330;
        const int PagerY = 36, PagerH = 24;
        const int ChartY = 122, ChartH = 116;
        const int PanelX = 340, PanelY = 38, PanelW = 132, PanelH = 226;

        // "Remove XYZ?" pop-up over the detail view.
        const int AskW = 300, AskH = 150, AskX = (Theme.ScreenWidth - AskW) / 2, AskY = 76;
        const int AskButtonW = 120, AskButtonH = 44, AskButtonY = AskY + AskH - 60;

        const int NoteSeconds = 6;
        const string Dash = "—";

        static readonly Color Background = Theme.PanelDark;
        static readonly Color Divider = Canvas.Lerp(Theme.PanelDark, Theme.Track, 0.45);
        static readonly Color GridLine = Canvas.Lerp(Theme.PanelDark, Theme.Track, 0.35);

        int page;
        string detail;          // symbol shown in the detail view, null for the watchlist
        int pagerLeft, pagerMid;

        readonly Keyboard keyboard = new Keyboard();
        bool adding;            // the keyboard is up for a new symbol
        bool confirmRemove;     // the "Remove XYZ?" pop-up is up over the detail view
        string note;            // e.g. "AMD added", shown in the status bar for a few seconds
        DateTime noteUntil;     // UTC

        public StocksScreen(Shell shell) : base(shell) { }

        public override string Title => this.adding ? "Add symbol" : this.detail != null ? "Watchlist" : "Stocks";

        public override int Stamp => this.adding ? 0 : this.State.StocksVersion + this.State.MarketVersion * 7;

        bool NoteShowing => this.note != null && Clock.UtcNow < this.noteUntil && (this.adding || this.detail == null);

        public override string BarText {
            get {
                var s = this.State;
                if (this.NoteShowing) return this.note;
                if (this.adding) return s.Stocks.Length + " of " + Config.MaxSymbols + " symbols";
                var text = s.HasMarketStatus ? Market.StatusText(s, Clock.UtcNow) : s.StocksMessage;
                if (this.detail == null && this.Pages > 1) text += "  ·  " + (this.page + 1) + "/" + this.Pages;
                return text;
            }
        }

        public override bool HasBarDot => !this.adding && !this.NoteShowing && this.State.HasMarketStatus;

        public override Color BarDotColor {
            get {
                var s = this.State;
                if (s.MarketOpen) return Theme.Up;
                return s.MarketSession == "pre-market" || s.MarketSession == "post-market" ? Theme.Accent : Theme.TextDim;
            }
        }

        /// <summary>+ on the watchlist, Remove on a stock's page, nothing while typing or asking.</summary>
        public override string BarButton => this.adding || this.confirmRemove ? null : this.detail != null ? "Remove" : "+";

        public override void BarButtonTap() {
            if (this.detail != null) this.confirmRemove = true;
            else this.StartAdding();
        }

        int Pages => (this.State.Stocks.Length + PerPage - 1) / PerPage;

        public override void Opened() {
            this.detail = null;
            this.page = 0;
            this.adding = false;
            this.confirmRemove = false;
            this.note = null;
        }

        public override bool Back() {
            if (this.adding) {
                this.adding = false;
                this.note = null;
                return true;
            }
            if (this.confirmRemove) {
                this.confirmRemove = false;
                return true;
            }
            if (this.detail == null) return false;
            this.detail = null;
            return true;
        }

        // -- Drawing -------------------------------------------------------------

        public override void Draw(Canvas c) {
            c.FillRect(Background, 0, Theme.BarHeight, Theme.ScreenWidth, Theme.ScreenHeight - Theme.BarHeight);
            c.FillRect(Divider, 0, Theme.BarHeight, Theme.ScreenWidth, 1);

            var s = this.State;
            if (this.adding) {
                c.FillRect(Theme.Background, 0, Theme.BarHeight + 1, Theme.ScreenWidth, Theme.ScreenHeight - Theme.BarHeight - 1);
                this.keyboard.Draw(c);
                return;
            }
            if (this.detail != null && s.FindStock(this.detail) is StockItem item) {
                this.DrawDetail(c, item);
                if (this.confirmRemove) DrawConfirm(c, item);
                return;
            }
            this.detail = null;
            this.confirmRemove = false;

            if (s.Stocks.Length == 0 || s.Config.FinnhubKey.Length == 0) {
                c.FillRoundRect(Theme.Panel, GridX, GridY + 2, Theme.ScreenWidth - GridX * 2, 120, CardRadius);
                c.DrawText(s.Config.FinnhubKey.Length == 0 ? "No Finnhub API key" : "No symbols to show", UiFont.Large, Theme.White, GridX + 16, GridY + 16);
                c.DrawText(s.Config.FinnhubKey.Length == 0 ? "Edit Portal\\stocks.txt on the SD card:" : "Tap + at the top to add one, or edit Portal\\stocks.txt:",
                    UiFont.Small, Theme.TextMuted, GridX + 16, GridY + 46);
                c.DrawText("apikey=your key from finnhub.io", UiFont.Small, Theme.White, GridX + 28, GridY + 68);
                c.DrawText("symbols=AAPL,MSFT,NVDA", UiFont.Small, Theme.White, GridX + 28, GridY + 88);
                return;
            }

            if (this.page >= this.Pages) this.page = 0;
            for (var i = 0; i < PerPage; i++) {
                var index = this.page * PerPage + i;
                if (index >= s.Stocks.Length) break;
                this.DrawCard(c, s.Stocks[index], CardX(i % Cols), CardY(i / Cols));
            }
        }

        static int CardX(int col) => GridX + col * (CardW + Gap);
        static int CardY(int row) => GridY + row * (CardH + Gap);

        void DrawCard(Canvas c, StockItem item, int x, int y) {
            c.FillRoundRect(Theme.Panel, x, y, CardW, CardH, CardRadius);
            const int inner = CardW - Pad * 2;
            var color = item.IsUp ? Theme.Up : Theme.Down;

            // Percent pill, tucked into the top right corner.
            var pillW = 0;
            if (item.HasQuote) {
                var pct = Format.Percent(item.ChangePercent);
                pillW = c.MeasureText(pct, UiFont.Pill) + 8;
                var px = x + CardW - 5 - pillW;
                c.FillPill(Tint(color), px, y + 7, pillW, 16);
                c.DrawText(pct, UiFont.Pill, Canvas.Lerp(color, Theme.White, 0.3), px + 4, y + 9);
            }

            // Symbol (a size smaller when it would run into the pill) and company name.
            var room = CardW - Pad - 5 - (pillW > 0 ? pillW + 3 : 0);
            var symbolFont = c.MeasureText(item.Symbol, UiFont.Large) <= room ? UiFont.Large :
                             c.MeasureText(item.Symbol, UiFont.Medium) <= room ? UiFont.Medium : UiFont.Small;
            var symbolY = symbolFont == UiFont.Large ? 6 : symbolFont == UiFont.Medium ? 8 : 9;
            c.DrawText(c.Ellipsize(item.Symbol, symbolFont, room), symbolFont, Theme.White, x + Pad, y + symbolY);
            c.DrawText(c.Ellipsize(item.Name, UiFont.Tiny, inner), UiFont.Tiny, Theme.TextMuted, x + Pad, y + 28);

            if (!item.HasQuote) {
                var message = item.Error ?? "Loading...";
                c.DrawText(c.Ellipsize(message, UiFont.Tiny, inner), UiFont.Tiny, item.Error != null ? Theme.Down : Theme.TextDim, x + Pad, y + 52);
                return;
            }

            var price = Format.Money(item.Current);
            var priceFont = c.MeasureText(price, UiFont.XLarge) <= inner ? UiFont.XLarge : UiFont.Large;
            c.DrawText(price, priceFont, Theme.White, x + Pad, y + (priceFont == UiFont.XLarge ? 44 : 47));
            c.DrawText(c.Ellipsize(ChangeText(item) + " today", UiFont.Tiny, inner), UiFont.Tiny, color, x + Pad, y + 69);

            SessionSpark(c, item, x + Pad, y + 88, inner, 14, color);
        }

        void DrawDetail(Canvas c, StockItem item) {
            var s = this.State;
            var color = item.IsUp ? Theme.Up : Theme.Down;

            this.DrawPager(c, item);

            // Symbol, then "Company · Exchange".
            c.DrawText(item.Symbol, UiFont.XLarge, Theme.White, MainX, 37);
            var subX = MainX + c.MeasureText(item.Symbol, UiFont.XLarge) + 10;
            // Shorten the name rather than the exchange.
            var exchange = item.Exchange.Length > 0 ? " · " + item.Exchange : "";
            var name = item.Name.Length > 0 && item.Name != item.Symbol ? item.Name : "";
            var room = this.pagerLeft - 10 - subX;
            var sub = name.Length > 0 ? c.Ellipsize(name, UiFont.Small, room - c.MeasureText(exchange, UiFont.Small)) + exchange : item.Exchange;
            c.DrawText(c.Ellipsize(sub, UiFont.Small, room), UiFont.Small, Theme.TextMuted, subX, 42);

            this.DrawStats(c, item, color);

            if (!item.HasQuote) {
                c.DrawText(c.Ellipsize(item.Error ?? "Waiting for the first quote...", UiFont.Large, MainRight - MainX), UiFont.Large,
                    item.Error != null ? Theme.Down : Theme.White, MainX, 76);
                return;
            }

            // Price and the day's change.
            var price = Format.Money(item.Current);
            c.DrawText(price, UiFont.Huge, Theme.White, MainX - 1, 60);
            var cx = MainX + c.MeasureText(price, UiFont.Huge) + 12;
            var change = ChangeText(item) + "  (" + Format.Percent(item.ChangePercent) + ")";
            var changeFont = cx + c.MeasureText(change, UiFont.XLarge) <= MainRight ? UiFont.XLarge : UiFont.Medium;
            c.DrawText(change, changeFont, color, cx, changeFont == UiFont.XLarge ? 70 : 74);

            if (item.Time > 0) {
                var et = Market.EasternFromUnix(item.Time);
                var text = "Last trade " + Format.ShortTime(et, s.Config.Use24Hour) + " ET, " +
                           Format.ShortDay(et.DayOfWeek) + " " + Format.ShortMonth(et.Month) + " " + et.Day;
                c.DrawText(text, UiFont.Tiny, Theme.TextMuted, MainX, 100);
            }

            this.DrawChart(c, item, MainX, ChartY, MainRight - MainX, ChartH, color);
        }

        /// <summary>"&lt; 3 / 8 &gt;" at the top right of the main area; tapping either half moves through the list.</summary>
        void DrawPager(Canvas c, StockItem item) {
            var stocks = this.State.Stocks;
            var index = 0;
            for (var i = 0; i < stocks.Length; i++) {
                if (stocks[i] == item) index = i;
            }
            var label = (index + 1) + " / " + stocks.Length;
            var w = c.MeasureText(label, UiFont.Medium);
            var cy = PagerY + 12;
            var right = MainRight - 2;
            Chevron(c, right, cy, 1);
            var lx = right - 12 - w;
            c.DrawText(label, UiFont.Medium, Theme.White, lx, PagerY + 4);
            Chevron(c, lx - 12, cy, -1);
            this.pagerLeft = lx - 18;
            this.pagerMid = lx + w / 2;
        }

        /// <summary>The pop-up that asks before a symbol comes off the watchlist.</summary>
        static void DrawConfirm(Canvas c, StockItem item) {
            c.FillRoundRect(Theme.Accent, AskX - 3, AskY - 3, AskW + 6, AskH + 6, 18);
            c.FillRoundRect(Theme.PanelDark, AskX, AskY, AskW, AskH, 16);
            const int cx = AskX + AskW / 2;
            c.DrawTextCentered(c.Ellipsize("Remove " + item.Symbol + "?", UiFont.XLarge, AskW - 24), UiFont.XLarge, Theme.White, cx, AskY + 18);
            c.DrawTextCentered("It comes off the watchlist and stocks.txt.", UiFont.Small, Theme.TextMuted, cx, AskY + 52);
            Widgets.Button(c, cx - AskButtonW - 8, AskButtonY, AskButtonW, AskButtonH, "Cancel", false, true);
            Widgets.Button(c, cx + 8, AskButtonY, AskButtonW, AskButtonH, "Remove", true, true);
        }

        static void Chevron(Canvas c, int tipX, int cy, int direction) {
            var backX = tipX - 4 * direction;
            c.ThickLine(Theme.AccentSoft, backX, cy - 5, tipX, cy, 2);
            c.ThickLine(Theme.AccentSoft, tipX, cy, backX, cy + 5, 2);
        }

        void DrawStats(Canvas c, StockItem item, Color color) {
            c.FillRoundRect(Theme.Panel, PanelX, PanelY, PanelW, PanelH, CardRadius);
            const int left = PanelX + 10, right = PanelX + PanelW - 10;
            var y = PanelY + 7;

            StatRow(c, "Open", PriceOrDash(item.Open), left, right, ref y);
            StatRow(c, "High", PriceOrDash(item.High), left, right, ref y);
            StatRow(c, "Low", PriceOrDash(item.Low), left, right, ref y);
            StatRow(c, "Prev close", PriceOrDash(item.PrevClose), left, right, ref y);
            y += 4;

            RangeBar(c, "Day range", item.Low, item.High, item.Current, left, right, color, ref y);
            RangeBar(c, "52-week range", item.Low52, item.High52, item.Current, left, right, color, ref y);
            y += 2;

            StatRow(c, "Market cap", item.MarketCap > 0 ? Format.MarketCap(item.MarketCap) : Dash, left, right, ref y);
            StatRow(c, "P/E", item.PE > 0 ? Format.Number(item.PE, 2) : Dash, left, right, ref y);
            StatRow(c, "Div. yield", item.DividendYield > 0 ? Format.Number(item.DividendYield, 2) + "%" : Dash, left, right, ref y);

            c.DrawText("Industry", UiFont.Tiny, Theme.TextMuted, left, y + 1);
            c.DrawText(c.Ellipsize(item.Industry.Length > 0 ? item.Industry : Dash, UiFont.Small, right - left), UiFont.Small, Theme.White, left, y + 14);
        }

        static void StatRow(Canvas c, string label, string value, int left, int right, ref int y) {
            c.DrawText(label, UiFont.Tiny, Theme.TextMuted, left, y + 1);
            c.DrawTextRight(value, UiFont.Small, Theme.White, right, y);
            y += 15;
        }

        /// <summary>Label, a track with a marker where the price sits, and the low/high under it.</summary>
        static void RangeBar(Canvas c, string label, double low, double high, double value, int left, int right, Color color, ref int y) {
            c.DrawText(label, UiFont.Tiny, Theme.TextMuted, left, y + 1);
            var by = y + 18;
            var w = right - left;
            c.FillRoundRect(Theme.Track, left, by, w, 4, 2);
            if (low > 0 && high >= low) {
                var f = high > low ? (value - low) / (high - low) : 0.5;
                if (f < 0) f = 0;
                if (f > 1) f = 1;
                c.FillRoundRect(color, left + (int)(f * (w - 4)), by - 3, 4, 10, 2);
                c.DrawText(Format.Price(low), UiFont.Tiny, Theme.TextMuted, left, by + 7);
                c.DrawTextRight(Format.Price(high), UiFont.Tiny, Theme.TextMuted, right, by + 7);
            }
            else {
                c.DrawText(Dash, UiFont.Tiny, Theme.TextMuted, left, by + 7);
            }
            y += 38;
        }

        // -- Charts ----------------------------------------------------------------

        /// <summary>The detail chart: price grid on the right, hours along the bottom, previous close dashed.</summary>
        void DrawChart(Canvas c, StockItem item, int x, int y, int w, int h, Color color) {
            double min, max;
            PriceRange(item, out min, out max);

            // Round grid steps (0.25, 0.5, 1, 2.5...) giving about three bands.
            var step = NiceStep((max - min) / 3);
            var k0 = (long)(min / step);
            if (k0 * step > min) k0--;
            var k1 = (long)(max / step);
            if (k1 * step < max - step * 1e-6) k1++;
            if (k1 <= k0) k1 = k0 + 1;
            var lo = k0 * step;
            var hi = k1 * step;
            var decimals = step < 0.01 ? 4 : 2;

            var labelW = 0;
            for (var k = k0; k <= k1; k++) labelW = Math.Max(labelW, c.MeasureText(Format.Number(k * step, decimals), UiFont.Tiny));
            var plotW = w - labelW - 8;

            for (var k = k0; k <= k1; k++) {
                var py = ScaleY(k * step, lo, hi, y, h);
                c.FillRect(GridLine, x, py, plotW + 4, 1);
                c.DrawText(Format.Number(k * step, decimals), UiFont.Tiny, Theme.TextMuted, x + plotW + 8, py - 7);
            }
            for (var hour = 10; hour <= 15; hour++) {
                c.FillRect(GridLine, SessionX(hour * 60, x, plotW), y, 1, h);
            }
            if (item.PrevClose > 0) Dashed(c, Theme.TextMuted, x, ScaleY(item.PrevClose, lo, hi, y, h), plotW, 5, 4);

            // Hours along the bottom (Eastern time).
            var use24 = this.State.Config.Use24Hour;
            var ty = y + h + 5;
            c.DrawText("9:30", UiFont.Tiny, Theme.TextMuted, x, ty);
            c.DrawTextCentered(use24 ? "11:00" : "11 AM", UiFont.Tiny, Theme.TextMuted, SessionX(11 * 60, x, plotW), ty);
            c.DrawTextCentered(use24 ? "13:00" : "1 PM", UiFont.Tiny, Theme.TextMuted, SessionX(13 * 60, x, plotW), ty);
            c.DrawTextCentered(use24 ? "15:00" : "3 PM", UiFont.Tiny, Theme.TextMuted, SessionX(15 * 60, x, plotW), ty);
            c.DrawTextRight(use24 ? "16:00" : "4 PM", UiFont.Tiny, Theme.TextMuted, x + plotW, ty);

            if (item.SampleCount == 0) {
                const string wait = "The chart fills in while the Portal runs.";
                var ww = c.MeasureText(wait, UiFont.Small) + 16;
                c.FillRoundRect(Theme.Panel, x + (plotW - ww) / 2, y + h / 2 - 13, ww, 24, 8);
                c.DrawTextCentered(wait, UiFont.Small, Theme.TextMuted, x + plotW / 2, y + h / 2 - 9);
                return;
            }

            var n = SessionPoints(item, x, plotW, lo, hi, y, h);
            var xs = PointsX;
            var ys = PointsY;

            // Shade under the line, then the line and a dot on the latest price.
            var shade = Canvas.Lerp(Background, color, 0.22);
            var bottom = y + h;
            for (var i = 0; i + 1 < n; i++) {
                var span = xs[i + 1] - xs[i];
                for (var col = 0; col < span; col++) {
                    var top = ys[i] + (ys[i + 1] - ys[i]) * col / span;
                    c.FillRect(shade, xs[i] + col, top, 1, bottom - top);
                }
            }
            var last = n - 1;
            c.FillRect(shade, xs[last], ys[last], 1, bottom - ys[last]);
            if (n > 1) c.Polyline(color, xs, ys, n, 2);
            c.FillCircle(color, xs[last], ys[last], 3);
        }

        /// <summary>The small card chart: the session so far, with the previous close dashed.</summary>
        static void SessionSpark(Canvas c, StockItem item, int x, int y, int w, int h, Color color) {
            double min, max;
            PriceRange(item, out min, out max);
            if (item.PrevClose > 0) Dashed(c, Theme.TextDim, x, ScaleY(item.PrevClose, min, max, y, h), w, 3, 3);
            var n = SessionPoints(item, x, w, min, max, y, h);
            if (n > 1) c.Polyline(color, PointsX, PointsY, n, 2);
            else if (n == 1) c.FillRect(color, PointsX[0], PointsY[0], 2, 2);
        }

        // Scratch space for chart points, reused by every chart (drawing is on the UI thread only),
        // so redraws don't leave garbage behind.
        static readonly int[] PointsX = new int[StockItem.MaxSamples];
        static readonly int[] PointsY = new int[StockItem.MaxSamples];

        /// <summary>Sample positions into PointsX/PointsY: x by time of day across the session, y by price.</summary>
        static int SessionPoints(StockItem item, int x, int w, double min, double max, int y, int h) {
            var times = item.SampleTimes;
            var prices = item.SamplePrices;
            var n = item.SampleCount;
            for (var i = 0; i < n; i++) {
                PointsX[i] = x + (int)(Market.SessionFraction(times[i]) * (w - 2));
                PointsY[i] = ScaleY(prices[i], min, max, y, h);
            }
            return n;
        }

        /// <summary>Lowest and highest of the samples, the current price and the previous close.</summary>
        static void PriceRange(StockItem item, out double min, out double max) {
            min = item.PrevClose > 0 ? item.PrevClose : item.Current;
            max = min;
            if (item.HasQuote) {
                if (item.Current < min) min = item.Current;
                if (item.Current > max) max = item.Current;
            }
            var prices = item.SamplePrices;
            for (var i = 0; i < item.SampleCount; i++) {
                if (prices[i] < min) min = prices[i];
                if (prices[i] > max) max = prices[i];
            }
            if (max - min < max * 0.002) {
                var pad = max * 0.001 + 0.0001;
                min -= pad;
                max += pad;
            }
        }

        static int ScaleY(double value, double min, double max, int y, int h) =>
            y + h - 1 - (int)((value - min) / (max - min) * (h - 1) + 0.5);

        static int SessionX(int minuteOfDay, int x, int w) =>
            x + (minuteOfDay - Market.OpenMinute) * (w - 2) / Market.SessionMinutes;

        static double NiceStep(double raw) {
            double mag = 1;
            while (mag * 10 <= raw) mag *= 10;
            while (mag > raw) mag /= 10;
            var f = raw / mag;
            return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * mag;
        }

        static void Dashed(Canvas c, Color color, int x, int y, int w, int dash, int gap) {
            for (var dx = 0; dx < w; dx += dash + gap) c.FillRect(color, x + dx, y, Math.Min(dash, w - dx), 1);
        }

        static Color Tint(Color color) => Canvas.Lerp(Theme.Panel, color, 0.3);

        static string ChangeText(StockItem item) => Format.Signed(item.Change, item.Current < 1 ? 4 : 2);

        static string PriceOrDash(double value) => value > 0 ? Format.Price(value) : Dash;

        // -- Touch -----------------------------------------------------------------

        public override void Tap(int x, int y) {
            if (this.adding) {
                this.KeyboardTap(x, y);
                return;
            }
            if (this.confirmRemove) {
                this.ConfirmTap(x, y);
                return;
            }
            if (this.detail != null) {
                if (y >= PagerY - 6 && y < PagerY + PagerH + 6 && x >= this.pagerLeft && x <= MainRight + 8) {
                    this.Swipe(x < this.pagerMid ? -1 : 1);
                }
                return;
            }
            if (x < GridX || y < GridY) return;
            var col = (x - GridX) / (CardW + Gap);
            var row = (y - GridY) / (CardH + Gap);
            if (col >= Cols || row >= Rows || !Inside(x, y, CardX(col), CardY(row), CardW, CardH)) return;
            var index = this.page * PerPage + row * Cols + col;
            if (index >= this.State.Stocks.Length) return;
            this.detail = this.State.Stocks[index].Symbol;
            this.Shell.Invalidate();
        }

        public override void Swipe(int direction) {
            if (this.adding || this.confirmRemove) return;
            var stocks = this.State.Stocks;
            if (this.detail != null) {
                // Next/previous symbol; keep the watchlist page in step with it.
                for (var i = 0; i < stocks.Length; i++) {
                    if (stocks[i].Symbol != this.detail) continue;
                    var next = (i + direction + stocks.Length) % stocks.Length;
                    this.detail = stocks[next].Symbol;
                    this.page = next / PerPage;
                    break;
                }
            }
            else if (this.Pages > 1) {
                this.page = (this.page + direction + this.Pages) % this.Pages;
            }
            this.Shell.Invalidate();
        }

        /// <summary>On the watchlist, slide up for the next page of cards and down for the previous one.</summary>
        public override void VerticalSwipe(int direction) {
            if (this.adding || this.confirmRemove || this.detail != null) return;
            var next = this.page + direction;
            if (next < 0 || next >= this.Pages) return;
            this.page = next;
            this.Shell.Invalidate();
        }

        // -- Adding and removing symbols ----------------------------------------------

        void StartAdding() {
            if (this.State.Config.Symbols.Length >= Config.MaxSymbols) {
                this.Note("The watchlist is full (" + Config.MaxSymbols + " symbols)");
                return;
            }
            this.adding = true;
            this.note = null;
            this.keyboard.Reset("Ticker symbol, e.g. AAPL or BRK.B", "Add", false, Config.MaxSymbolLength);
            this.keyboard.Capitals = true;
        }

        void KeyboardTap(int x, int y) {
            var result = this.keyboard.Tap(x, y);
            if (result == Keyboard.None) return;
            if (result == Keyboard.Done) this.Add(this.keyboard.Text);
            this.Shell.Invalidate();
        }

        /// <summary>Adds a typed symbol at the end of the list and shows the page its card is on.</summary>
        void Add(string text) {
            var s = this.State;
            if (text.Trim().Length == 0) {
                this.adding = false;   // nothing typed: same as going back
                return;
            }
            var symbol = Config.CleanSymbol(text);
            if (symbol == null) {
                this.Note("Use letters, digits and . - : only");
                return;
            }
            this.adding = false;
            if (s.Config.HasSymbol(symbol)) {
                this.Note(symbol + " is already on the watchlist");
            }
            else {
                var old = s.Config.Symbols;
                var list = new string[old.Length + 1];
                Array.Copy(old, list, old.Length);
                list[old.Length] = symbol;
                this.Save(list, symbol + " added");
                this.Shell.Host.RefreshStocks();   // quote it now rather than at the next round
            }
            for (var i = 0; i < s.Stocks.Length; i++) {
                if (s.Stocks[i].Symbol == symbol) this.page = i / PerPage;
            }
        }

        void ConfirmTap(int x, int y) {
            if (y < AskButtonY - 6 || y >= AskButtonY + AskButtonH + 6) return;
            const int cx = AskX + AskW / 2;
            if (x >= cx - AskButtonW - 14 && x < cx) {
                this.confirmRemove = false;
            }
            else if (x >= cx && x < cx + AskButtonW + 14) {
                this.Remove(this.detail);
            }
            else {
                return;
            }
            this.Shell.Invalidate();
        }

        /// <summary>Takes a symbol off the list and goes back to the watchlist page it was on.</summary>
        void Remove(string symbol) {
            this.confirmRemove = false;
            var old = this.State.Config.Symbols;
            var list = new string[old.Length > 0 ? old.Length - 1 : 0];
            var n = 0;
            var found = false;
            for (var i = 0; i < old.Length; i++) {
                if (old[i] == symbol) found = true;
                else if (n < list.Length) list[n++] = old[i];
            }
            if (!found) return;
            this.Save(list, symbol + " removed");
            this.detail = null;
            if (this.page >= this.Pages) this.page = this.Pages > 0 ? this.Pages - 1 : 0;
        }

        /// <summary>Puts the new list in place and writes it to stocks.txt.</summary>
        void Save(string[] symbols, string done) {
            var s = this.State;
            var text = s.Config.SetSymbols(symbols);
            s.SetStocksFromSymbols();
            this.Note(this.Shell.Host.SaveSettings(DefaultFiles.StocksName, text) ? done : done + ", no SD card to save it");
        }

        void Note(string text) {
            this.note = text;
            this.noteUntil = Clock.UtcNow.AddSeconds(NoteSeconds);
            this.Shell.Invalidate();
        }
    }
}
