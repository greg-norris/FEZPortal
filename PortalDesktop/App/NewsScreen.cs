using System;
using System.Collections;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// The latest headlines from newsdata.io, four to a page: the headline (two lines at most),
    /// the source and how long ago it was published. The arrows on the right (or a swipe) turn
    /// the pages, a tap on a headline opens its summary, and Refresh in the status bar gets new
    /// headlines. Opened from the News button on the desktop.
    /// </summary>
    public class NewsScreen : Screen {
        public const int PerPage = 4;
        const int ListX = Theme.Margin, ListY = 36, RowH = 54, RowGap = 4, Pad = 10;
        const int ListH = PerPage * RowH + (PerPage - 1) * RowGap;
        const int ArrowW = 40, ArrowH = 44, PagerX = Theme.ScreenWidth - Theme.Margin - ArrowW;

        int page;

        // Wrapped headlines, kept until the headlines or the row width change (Wrap measures every word).
        NewsItem[] wrappedFor;
        int wrappedWidth;
        ArrayList[] wrapped;

        public NewsScreen(Shell shell) : base(shell) { }

        public override string Title => "News";

        public override int Stamp => this.State.NewsVersion;

        public override string BarText {
            get {
                var s = this.State;
                // "Refreshing...", or why the last update failed (the headlines shown stay).
                if (s.News.Length > 0 && s.NewsMessage.Length > 0) return s.NewsMessage;
                if (s.NewsUpdated == DateTime.MinValue) return null;
                return "Updated " + Format.ShortTime(Clock.ToLocal(s.NewsUpdated), s.Config.Use24Hour);
            }
        }

        public override string BarButton => this.State.Config.NewsKey.Length > 0 ? "Refresh" : null;

        public override void BarButtonTap() {
            this.State.NewsMessage = "Refreshing...";
            this.State.NewsVersion++;
            this.Shell.Host.RefreshNews();
        }

        public override void Opened() => this.page = 0;

        int Pages => (this.State.News.Length + PerPage - 1) / PerPage;

        /// <summary>Turns to the page with headline <paramref name="index"/> (after paging through them one by one).</summary>
        public void ShowPageOf(int index) => this.page = index / PerPage;

        // -- Drawing ---------------------------------------------------------------------

        public override void Draw(Canvas c) {
            var s = this.State;
            var items = s.News;
            if (items.Length == 0) {
                this.DrawEmpty(c, s);
                return;
            }
            var pages = this.Pages;
            if (this.page >= pages) this.page = pages - 1;
            var rowW = pages > 1 ? PagerX - 8 - ListX : Theme.ScreenWidth - Theme.Margin * 2;
            var textW = rowW - Pad * 2;
            if (this.wrappedFor != items || this.wrappedWidth != textW) {
                this.wrapped = new ArrayList[items.Length];
                this.wrappedFor = items;
                this.wrappedWidth = textW;
            }

            var nowUtc = Clock.UtcNow;
            var titleH = c.LineHeight(UiFont.Small);
            for (var row = 0; row < PerPage; row++) {
                var i = this.page * PerPage + row;
                if (i >= items.Length) break;
                var item = items[i];
                var y = ListY + row * (RowH + RowGap);
                Card(c, ListX, y, rowW, RowH);
                var x = ListX + Pad;

                if (this.wrapped[i] == null) this.wrapped[i] = c.Wrap(item.Title, UiFont.Small, textW);
                var lines = this.wrapped[i];
                for (var k = 0; k < lines.Count && k < 2; k++) {
                    var line = (string)lines[k];
                    if (k == 1 && lines.Count > 2) line = c.Ellipsize(line + " " + (string)lines[2], UiFont.Small, textW);
                    c.DrawText(line, UiFont.Small, Theme.White, x, y + 5 + k * titleH);
                }

                // Source in yellow, then how long ago.
                var metaY = y + RowH - 5 - c.LineHeight(UiFont.Tiny);
                var age = NewsData.Age(item.Published, nowUtc);
                var ageText = age.Length > 0 ? "  ·  " + age : "";
                var source = c.Ellipsize(item.Source, UiFont.Tiny, textW - c.MeasureText(ageText, UiFont.Tiny));
                c.DrawText(source, UiFont.Tiny, Theme.Accent, x, metaY);
                if (ageText.Length > 0) c.DrawText(ageText, UiFont.Tiny, Theme.TextMuted, x + c.MeasureText(source, UiFont.Tiny), metaY);
            }

            if (pages > 1) {
                var bottom = ListY + ListH - ArrowH;
                Arrow(c, PagerX, ListY, true, this.page > 0);
                Arrow(c, PagerX, bottom, false, this.page < pages - 1);
                var midY = (ListY + ArrowH + bottom) / 2;
                c.DrawTextCentered((this.page + 1).ToString(), UiFont.Large, Theme.White, PagerX + ArrowW / 2, midY - c.LineHeight(UiFont.Large));
                c.DrawTextCentered("of " + pages, UiFont.Small, Theme.TextMuted, PagerX + ArrowW / 2, midY + 2);
            }
        }

        void DrawEmpty(Canvas c, PortalState s) {
            const int h = ListH;
            var w = Theme.ScreenWidth - Theme.Margin * 2;
            Card(c, ListX, ListY, w, h);
            var cx = ListX + w / 2;
            Icons.Newspaper(c, cx, ListY + 62, 34);
            c.DrawTextCentered("No news yet", UiFont.XLarge, Theme.White, cx, ListY + 112);
            var y = ListY + 142;
            var lines = c.Wrap(s.NewsMessage, UiFont.Small, w - 60);
            for (var i = 0; i < lines.Count && i < 3; i++) {
                c.DrawTextCentered((string)lines[i], UiFont.Small, Theme.TextMuted, cx, y);
                y += c.LineHeight(UiFont.Small) + 3;
            }
        }

        static void Arrow(Canvas c, int x, int y, bool up, bool enabled) {
            c.FillRoundRect(enabled ? Theme.Panel : Theme.PanelDark, x, y, ArrowW, ArrowH, 10);
            var color = enabled ? Theme.White : Theme.TextDim;
            var cx = x + ArrowW / 2;
            var cy = y + ArrowH / 2;
            if (up) c.FillTriangle(color, cx - 9, cy + 4, cx + 9, cy + 4, cx, cy - 6);
            else c.FillTriangle(color, cx - 9, cy - 4, cx + 9, cy - 4, cx, cy + 6);
        }

        /// <summary>Once a minute: redraw, so "5 min ago" stays right.</summary>
        public override void Tick(Canvas c, DateTime now, DateTime previous) {
            if (now.Minute != previous.Minute && this.State.News.Length > 0) this.Shell.Invalidate();
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            var items = this.State.News;
            if (items.Length == 0) return;
            var pages = this.Pages;
            if (pages > 1 && x >= PagerX - 4) {
                if (y < ListY + ListH / 2) this.Turn(-1);
                else this.Turn(1);
                return;
            }
            if (y < ListY) return;
            var row = (y - ListY) / (RowH + RowGap);
            var i = this.page * PerPage + row;
            if (row < PerPage && i < items.Length) this.Shell.ArticleApp.Show(items, i);
        }

        /// <summary>Swipe left (or up) for the next page, right (or down) for the previous one.</summary>
        public override void Swipe(int direction) => this.Turn(direction);

        public override void VerticalSwipe(int direction) => this.Turn(direction);

        void Turn(int direction) {
            var next = this.page + direction;
            if (next < 0 || next >= this.Pages) return;
            this.page = next;
            this.Shell.Invalidate();
        }
    }
}
