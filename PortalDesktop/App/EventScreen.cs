using System;
using System.Collections;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// One calendar event in full: title, day and time (start - end), location and the message
    /// from the online calendar. Long messages page with the arrows or a swipe.
    /// Opened by tapping an event on the Calendar page; back returns there.
    /// </summary>
    public class EventScreen : Screen {
        const int CardX = Theme.Margin, CardY = 36, CardW = Theme.ScreenWidth - Theme.Margin * 2, CardH = 228;
        const int Pad = 14, ArrowW = 40, ArrowH = 36, LineGap = 3;

        CalendarEvent ev;
        DateTime day;
        ArrayList body;         // the message, wrapped (built on first draw)
        int page, pages, linesPerPage, bodyTop;

        public EventScreen(Shell shell) : base(shell) { }

        public override string Title => "Event";

        public override Screen BackTo => this.Shell.CalendarApp;

        public void Show(CalendarEvent e, DateTime date) {
            this.ev = e;
            this.day = date;
            this.body = null;
            this.page = 0;
            this.Shell.Show(this);
        }

        // -- Drawing -------------------------------------------------------------------

        public override void Draw(Canvas c) {
            Card(c, CardX, CardY, CardW, CardH);
            var e = this.ev;
            if (e == null) return;
            var x = CardX + Pad;
            var width = CardW - Pad * 2;
            var y = CardY + 10;

            // Title: two lines of the big font, or up to three of a smaller one for long titles.
            var titleFont = UiFont.XLarge;
            var maxLines = 2;
            var title = c.Wrap(e.Text, titleFont, width);
            if (title.Count > 2) {
                titleFont = UiFont.Large;
                maxLines = 3;
                title = c.Wrap(e.Text, titleFont, width);
            }
            for (var i = 0; i < title.Count && i < maxLines; i++) {
                var line = (string)title[i];
                if (i == maxLines - 1 && title.Count > maxLines) line = c.Ellipsize(line + " " + (string)title[maxLines], titleFont, width);
                c.DrawText(line, titleFont, Theme.White, x, y);
                y += c.LineHeight(titleFont);
            }

            // When, and where.
            y += 2;
            c.DrawText(c.Ellipsize(this.When(), UiFont.Medium, width), UiFont.Medium, Theme.Accent, x, y);
            y += c.LineHeight(UiFont.Medium) + 2;
            if (e.Location.Length > 0) {
                c.DrawText(c.Ellipsize(e.Location, UiFont.Small, width), UiFont.Small, Theme.TextMuted, x, y);
                y += c.LineHeight(UiFont.Small) + 2;
            }
            y += 4;
            c.FillRect(Theme.PanelLight, x, y, width, 1);
            y += 8;
            this.bodyTop = y;

            // The message, a page at a time.
            if (e.Description.Length == 0) {
                c.DrawText(e.Hour < 0 && e.EndHour < 0 && e.Location.Length == 0 ? "No details for this event." : "No message.",
                    UiFont.Small, Theme.TextDim, x, y);
                this.pages = 1;
                return;
            }
            var lineH = c.LineHeight(UiFont.Small) + LineGap;
            this.linesPerPage = Math.Max(1, (CardY + CardH - 8 - y) / lineH);
            var textW = width - ArrowW - 10;   // room for the page arrows, if needed
            if (this.body == null) {
                this.body = c.Wrap(e.Description, UiFont.Small, width);
                if (this.body.Count > this.linesPerPage) this.body = c.Wrap(e.Description, UiFont.Small, textW);
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

        /// <summary>"Tuesday, November 17 · 4:00 PM - 5:00 PM" or "... · All day".</summary>
        string When() {
            var e = this.ev;
            var use24 = this.State.Config.Use24Hour;
            var date = Format.LongDate(this.day);
            if (e.Hour < 0) return date + " · All day";
            var start = Format.ShortTime(new DateTime(2000, 1, 1, e.Hour, e.Minute, 0), use24);
            if (e.EndHour < 0) return date + " · " + start;
            return date + " · " + start + " — " + Format.ShortTime(new DateTime(2000, 1, 1, e.EndHour, e.EndMinute, 0), use24);
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            if (this.pages <= 1) return;
            var ax = CardX + CardW - Pad - ArrowW;
            if (x < ax - 6) return;
            if (y >= this.bodyTop - 4 && y < this.bodyTop + ArrowH + 4) this.Swipe(-1);
            else if (y >= CardY + CardH - 8 - ArrowH - 4 && y < CardY + CardH) this.Swipe(1);
        }

        /// <summary>Swipe left for the next page of the message, right for the previous one.</summary>
        public override void Swipe(int direction) {
            var next = this.page + direction;
            if (next < 0 || next >= this.pages) return;
            this.page = next;
            this.Shell.Invalidate();
        }
    }
}
