using System;
using System.Collections;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>Month view with today highlighted, dots on days with events, and the selected day's events.</summary>
    public class CalendarScreen : Screen {
        const int GridCardX = Theme.Margin, GridCardY = 38, GridCardW = 300, GridCardH = 226;
        const int CellW = 40, CellH = 29;
        const int GridX = GridCardX + 10, GridY = GridCardY + 50;
        const int DayCardX = 316, DayCardY = 38, DayCardW = 156, DayCardH = 226;
        const int TodayX = DayCardX + 12, TodayY = DayCardY + DayCardH - 34, TodayW = 70, TodayH = 24;
        const int ListY = DayCardY + 66, PerEvent = 32, ListRows = (TodayY - 6 - ListY) / PerEvent;

        int viewYear, viewMonth;
        DateTime selected;

        public CalendarScreen(Shell shell) : base(shell) { }

        public override string Title => "Calendar";

        public override int Stamp => this.State.CalendarVersion;

        /// <summary>Online calendar status: "Synced 10:42 AM", "Syncing...", or what went wrong.</summary>
        public override string BarText {
            get {
                var s = this.State;
                if (s.Config.CalendarUrls.Count == 0) return "No ics= link in calendar.txt";
                if (s.CalendarSyncing) return "Syncing calendar...";
                if (s.CalendarMessage.Length > 0) return s.CalendarMessage;
                if (s.CalendarUpdated == DateTime.MinValue) return s.Wifi == WifiStatus.Connected ? "Waiting to sync" : "Offline";
                return "Synced " + Format.ShortTime(Clock.ToLocal(s.CalendarUpdated), s.Config.Use24Hour) + " (tap to sync)";
            }
        }

        public override void Opened() => this.GoToToday();

        public override void Draw(Canvas c) {
            if (this.viewYear == 0) this.GoToToday();
            this.DrawMonth(c);
            this.DrawDay(c);
        }

        public override void Tap(int x, int y) {
            // An event in the day list: its page, with the full title and message.
            if (Inside(x, y, DayCardX, ListY - 4, DayCardW, ListRows * PerEvent)) {
                var list = CalendarEvents.On(this.State.Events, this.selected.Year, this.selected.Month, this.selected.Day);
                var row = (y - ListY + 4) / PerEvent;
                var isMoreRow = row == ListRows - 1 && list.Count > ListRows;   // "+N more" isn't one event
                if (row < list.Count && !isMoreRow) this.Shell.EventApp.Show((CalendarEvent)list[row], this.selected);
                return;
            }
            // The sync status in the status bar: sync now.
            if (y < Theme.BarHeight) {
                if (this.State.Config.CalendarUrls.Count > 0 && !this.State.CalendarSyncing) this.Shell.Host.RefreshCalendar();
                return;
            }
            if (Inside(x, y, GridCardX, GridCardY, 60, 40)) { this.ChangeMonth(-1); return; }
            if (Inside(x, y, GridCardX + GridCardW - 60, GridCardY, 60, 40)) { this.ChangeMonth(1); return; }
            if (Inside(x, y, TodayX, TodayY - 6, TodayW, TodayH + 12)) { this.GoToToday(); this.Shell.Invalidate(); return; }

            if (Inside(x, y, GridX, GridY, CellW * 7, CellH * 6)) {
                var cell = (y - GridY) / CellH * 7 + (x - GridX) / CellW;
                var day = cell - this.FirstColumn() + 1;
                if (day >= 1 && day <= DateTime.DaysInMonth(this.viewYear, this.viewMonth)) {
                    this.selected = new DateTime(this.viewYear, this.viewMonth, day);
                    this.Shell.Invalidate();
                }
            }
        }

        public override void Swipe(int direction) => this.ChangeMonth(direction);

        void GoToToday() {
            var now = Clock.Now;
            this.viewYear = now.Year;
            this.viewMonth = now.Month;
            this.selected = new DateTime(now.Year, now.Month, now.Day);
        }

        void ChangeMonth(int delta) {
            this.viewMonth += delta;
            if (this.viewMonth < 1) { this.viewMonth = 12; this.viewYear--; }
            if (this.viewMonth > 12) { this.viewMonth = 1; this.viewYear++; }
            // Keep a day selected in the month on screen (today if it's this month, else the 1st).
            var now = Clock.Now;
            this.selected = now.Year == this.viewYear && now.Month == this.viewMonth
                ? new DateTime(now.Year, now.Month, now.Day)
                : new DateTime(this.viewYear, this.viewMonth, 1);
            this.Shell.Invalidate();
        }

        int FirstColumn() => (int)new DateTime(this.viewYear, this.viewMonth, 1).DayOfWeek;

        void DrawMonth(Canvas c) {
            var events = this.State.Events;
            var now = Clock.Now;
            Card(c, GridCardX, GridCardY, GridCardW, GridCardH);

            Icons.Back(c, GridCardX + 16, GridCardY + 20, Theme.Accent);
            Icons.Forward(c, GridCardX + GridCardW - 26, GridCardY + 20, Theme.Accent);
            c.DrawTextCentered(Format.MonthName(this.viewMonth) + " " + this.viewYear, UiFont.Large, Theme.White, GridCardX + GridCardW / 2, GridCardY + 10);

            var letters = "SMTWTFS";
            for (var i = 0; i < 7; i++) {
                c.DrawTextCentered(letters.Substring(i, 1), UiFont.Tiny, Theme.TextDim, GridX + i * CellW + CellW / 2, GridY - 18);
            }

            var first = this.FirstColumn();
            var days = DateTime.DaysInMonth(this.viewYear, this.viewMonth);
            var lh = c.LineHeight(UiFont.Small);
            for (var d = 1; d <= days; d++) {
                var cell = first + d - 1;
                var cx = GridX + cell % 7 * CellW + CellW / 2;
                var cy = GridY + cell / 7 * CellH + CellH / 2;
                var isToday = now.Year == this.viewYear && now.Month == this.viewMonth && now.Day == d;
                var isSelected = this.selected.Year == this.viewYear && this.selected.Month == this.viewMonth && this.selected.Day == d;
                var weekend = cell % 7 == 0 || cell % 7 == 6;

                // The digits' ink runs from 3 rows below the line top to 4 rows above its bottom;
                // the circle, ring and box are all centred on that ink, not on the cell.
                var label = d.ToString();
                var textY = cy - lh / 2 - 1;
                var inkTop = textY + 3;
                var inkBottom = textY + lh - 4;              // last ink row
                var inkY = (inkTop + inkBottom) / 2;

                var textColor = weekend ? Theme.TextMuted : Theme.White;
                if (isToday) {
                    c.FillCircle(Theme.Accent, cx, inkY, 13);
                    textColor = Theme.PanelDark;
                }
                else if (isSelected) {
                    c.DrawCircle(Theme.AccentSoft, cx, inkY, 13, 2);
                }
                c.DrawTextCentered(label, UiFont.Small, textColor, cx, textY);

                if (CalendarEvents.Any(events, this.viewYear, this.viewMonth, d)) {
                    // Days with events: a 2-pixel box around the number, with the same space on every
                    // side (dark, and a pixel tighter so it fits inside, on today's yellow circle).
                    const int border = 2;
                    var space = isToday ? 1 : 2;
                    // The text width already includes about a pixel of side spacing per edge.
                    var boxW = c.MeasureText(label, UiFont.Small) + 2 * (space - 1 + border);
                    var boxTop = inkTop - space - border;
                    var boxH = inkBottom - inkTop + 1 + 2 * (space + border);
                    OutlineBox(c, isToday ? Theme.PanelDark : Theme.Accent, cx - boxW / 2, boxTop, boxW, boxH, border);
                }
            }
        }

        /// <summary>A rectangle outline, leaving whatever is inside showing.</summary>
        static void OutlineBox(Canvas c, System.Drawing.Color color, int x, int y, int w, int h, int t) {
            c.FillRect(color, x, y, w, t);
            c.FillRect(color, x, y + h - t, w, t);
            c.FillRect(color, x, y + t, t, h - 2 * t);
            c.FillRect(color, x + w - t, y + t, t, h - 2 * t);
        }

        void DrawDay(Canvas c) {
            var x = DayCardX + 12;
            var width = DayCardW - 24;
            Card(c, DayCardX, DayCardY, DayCardW, DayCardH);
            c.DrawText(Format.DayName(this.selected.DayOfWeek), UiFont.Medium, Theme.TextMuted, x, DayCardY + 10);
            c.DrawText(Format.ShortMonth(this.selected.Month) + " " + this.selected.Day, UiFont.XLarge, Theme.White, x, DayCardY + 28);
            c.FillRect(Theme.PanelLight, x, DayCardY + 58, width, 1);

            var list = CalendarEvents.On(this.State.Events, this.selected.Year, this.selected.Month, this.selected.Day);
            var y = ListY;
            if (list.Count == 0) {
                c.DrawText("No events", UiFont.Small, Theme.TextMuted, x, y);
                if (this.State.Config.CalendarUrls.Count == 0) {
                    c.DrawText("Add events to", UiFont.Tiny, Theme.TextDim, x, y + 24);
                    c.DrawText("Portal\\calendar.txt", UiFont.Tiny, Theme.TextDim, x, y + 38);
                    c.DrawText("on the SD card.", UiFont.Tiny, Theme.TextDim, x, y + 52);
                }
            }
            else {
                const int perEvent = PerEvent;
                const int room = ListRows;
                for (var i = 0; i < list.Count && i < room; i++) {
                    var e = (CalendarEvent)list[i];
                    if (i == room - 1 && list.Count > room) {
                        c.DrawText("+" + (list.Count - i) + " more", UiFont.Small, Theme.TextMuted, x, y + 8);
                        break;
                    }
                    var when = e.Hour < 0 ? "All day" : Format.ShortTime(new DateTime(2000, 1, 1, e.Hour, e.Minute, 0), this.State.Config.Use24Hour);
                    c.DrawText(when, UiFont.Tiny, Theme.Accent, x, y);
                    c.DrawText(c.Ellipsize(e.Text, UiFont.Small, width), UiFont.Small, Theme.White, x, y + 13);
                    y += perEvent;
                }
            }

            c.FillPill(Theme.Accent, TodayX, TodayY, TodayW, TodayH);
            c.DrawTextCentered("Today", UiFont.Small, Theme.PanelDark, TodayX + TodayW / 2, TodayY + 5);
        }
    }
}
