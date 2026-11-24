using System;
using System.Collections;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// One headline in full: the headline, source and age, and the summary from newsdata.io.
    /// Long summaries page with the arrows or a swipe up/down; swipe left/right (or Next in the
    /// status bar) for the next or previous headline. Opened from the News page; back returns there.
    /// </summary>
    public class ArticleScreen : Screen {
        const int CardX = Theme.Margin, CardY = 36, CardW = Theme.ScreenWidth - Theme.Margin * 2, CardH = 228;
        const int Pad = 14, ArrowW = 40, ArrowH = 36, LineGap = 3;

        NewsItem[] items;       // the headlines this one was opened from (kept if new ones arrive meanwhile)
        int index;
        ArrayList body;         // the summary, wrapped (built on first draw)
        int page, pages, linesPerPage, bodyTop;

        public ArticleScreen(Shell shell) : base(shell) { }

        public override string Title => "News";

        public override Screen BackTo => this.Shell.NewsApp;

        public override string BarText => this.items == null ? null : (this.index + 1) + " of " + this.items.Length;

        public override string BarButton => this.items != null && this.index < this.items.Length - 1 ? "Next" : null;

        public override void BarButtonTap() => this.Swipe(1);

        public void Show(NewsItem[] list, int i) {
            this.items = list;
            this.Open(i);
            this.Shell.Show(this);
        }

        void Open(int i) {
            this.index = i;
            this.body = null;
            this.page = 0;
            this.Shell.NewsApp.ShowPageOf(i);
        }

        // -- Drawing -------------------------------------------------------------------

        public override void Draw(Canvas c) {
            Card(c, CardX, CardY, CardW, CardH);
            if (this.items == null || this.index >= this.items.Length) return;
            var item = this.items[this.index];
            var x = CardX + Pad;
            var width = CardW - Pad * 2;
            var y = CardY + 10;

            // Headline: two lines of the big font, or up to three of a smaller one for long ones.
            var titleFont = UiFont.XLarge;
            var maxLines = 2;
            var title = c.Wrap(item.Title, titleFont, width);
            if (title.Count > 2) {
                titleFont = UiFont.Large;
                maxLines = 3;
                title = c.Wrap(item.Title, titleFont, width);
            }
            for (var i = 0; i < title.Count && i < maxLines; i++) {
                var line = (string)title[i];
                if (i == maxLines - 1 && title.Count > maxLines) line = c.Ellipsize(line + " " + (string)title[maxLines], titleFont, width);
                c.DrawText(line, titleFont, Theme.White, x, y);
                y += c.LineHeight(titleFont);
            }

            // Source and when.
            y += 2;
            var meta = item.Source;
            if (item.Published != DateTime.MinValue) {
                var local = Clock.ToLocal(item.Published);
                var when = Format.ShortTime(local, this.State.Config.Use24Hour);
                if (local.Date != Clock.Now.Date) when = Format.ShortDate(local) + ", " + when;
                meta += (meta.Length > 0 ? " · " : "") + when + " (" + NewsData.Age(item.Published, Clock.UtcNow).ToLower() + ")";
            }
            c.DrawText(c.Ellipsize(meta, UiFont.Medium, width), UiFont.Medium, Theme.Accent, x, y);
            y += c.LineHeight(UiFont.Medium) + 6;
            c.FillRect(Theme.PanelLight, x, y, width, 1);
            y += 8;
            this.bodyTop = y;

            // The summary, a page at a time.
            if (item.Description.Length == 0) {
                c.DrawText("No summary for this story.", UiFont.Small, Theme.TextDim, x, y);
                this.pages = 1;
                return;
            }
            var lineH = c.LineHeight(UiFont.Small) + LineGap;
            this.linesPerPage = Math.Max(1, (CardY + CardH - 8 - y) / lineH);
            var textW = width - ArrowW - 10;   // room for the page arrows, if needed
            if (this.body == null) {
                this.body = c.Wrap(item.Description, UiFont.Small, width);
                if (this.body.Count > this.linesPerPage) this.body = c.Wrap(item.Description, UiFont.Small, textW);
            }
            this.pages = (this.body.Count + this.linesPerPage - 1) / this.linesPerPage;
            if (this.page >= this.pages) this.page = this.pages - 1;

            var first = this.page * this.linesPerPage;
            for (var i = first; i < this.body.Count && i < first + this.linesPerPage; i++) {
                c.DrawText((string)this.body[i], UiFont.Small, Theme.White, x, y);
                y += lineH;
            }

            if (this.pages > 1) {
                var ax = CardX + CardW - Pad - ArrowW;
                var bottom = CardY + CardH - 8 - ArrowH;
                Arrow(c, ax, this.bodyTop, true, this.page > 0);
                Arrow(c, ax, bottom, false, this.page < this.pages - 1);
                c.DrawTextCentered((this.page + 1) + "/" + this.pages, UiFont.Small, Theme.TextMuted, ax + ArrowW / 2,
                    (this.bodyTop + ArrowH + bottom) / 2 - c.LineHeight(UiFont.Small) / 2);
            }
        }

        static void Arrow(Canvas c, int x, int y, bool up, bool enabled) {
            c.FillRoundRect(enabled ? Theme.PanelLight : Theme.PanelDark, x, y, ArrowW, ArrowH, 8);
            var color = enabled ? Theme.White : Theme.TextDim;
            var cx = x + ArrowW / 2;
            var cy = y + ArrowH / 2;
            if (up) c.FillTriangle(color, cx - 9, cy + 4, cx + 9, cy + 4, cx, cy - 6);
            else c.FillTriangle(color, cx - 9, cy - 4, cx + 9, cy - 4, cx, cy + 6);
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            if (this.pages <= 1) return;
            var ax = CardX + CardW - Pad - ArrowW;
            if (x < ax - 6) return;
            if (y >= this.bodyTop - 4 && y < this.bodyTop + ArrowH + 4) this.VerticalSwipe(-1);
            else if (y >= CardY + CardH - 8 - ArrowH - 4 && y < CardY + CardH) this.VerticalSwipe(1);
        }

        /// <summary>Swipe up for the next page of the summary, down for the previous one.</summary>
        public override void VerticalSwipe(int direction) {
            var next = this.page + direction;
            if (next < 0 || next >= this.pages) return;
            this.page = next;
            this.Shell.Invalidate();
        }

        /// <summary>Swipe left for the next headline, right for the previous one.</summary>
        public override void Swipe(int direction) {
            if (this.items == null) return;
            var next = this.index + direction;
            if (next < 0 || next >= this.items.Length) return;
            this.Open(next);
            this.Shell.Invalidate();
        }
    }
}
