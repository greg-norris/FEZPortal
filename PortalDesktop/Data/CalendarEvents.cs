using System;
using System.Collections;

namespace PortalDesktop.Data {
    /// <summary>One line of calendar.txt.</summary>
    public class CalendarEvent {
        public int Year;          // 0 = every year
        public int Month;
        public int Day;
        public int Hour = -1;     // -1 = all day
        public int Minute;
        public string Text = "";

        // Details (online calendars only; empty otherwise).
        public int EndHour = -1, EndMinute;     // -1 = no end time
        public string Location = "";
        public string Description = "";

        public bool IsOn(int year, int month, int day) => (this.Year == 0 || this.Year == year) && this.Month == month && this.Day == day;
    }

    /// <summary>
    /// Reads calendar.txt:
    ///   2026-11-20 12:30 Team lunch
    ///   2026-11-26 Thanksgiving
    ///   12-25 Christmas Day          (every year)
    /// </summary>
    public static class CalendarEvents {
        public static ArrayList Parse(string text) {
            var result = new ArrayList();
            if (text == null) return result;
            foreach (var raw in text.Split('\n')) {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var e = ParseLine(line);
                if (e != null) result.Add(e);
            }
            return result;
        }

        /// <summary>The ics=https://... lines: online calendars (Outlook, Google, iCloud "secret address" links).</summary>
        public static ArrayList Urls(string text) {
            var result = new ArrayList();
            if (text == null) return result;
            foreach (var raw in text.Split('\n')) {
                var line = raw.Trim();
                if (line.Length > 4 && line.Substring(0, 4).ToLower() == "ics=") {
                    var url = line.Substring(4).Trim();
                    // webcal:// is the same feed over https.
                    if (url.Length > 9 && url.Substring(0, 9).ToLower() == "webcal://") url = "https://" + url.Substring(9);
                    if (url.Length > 0) result.Add(url);
                }
            }
            return result;
        }

        /// <summary>Events in calendar.txt format (used to cache the online calendar on the SD card).</summary>
        public static string ToText(ArrayList events) {
            var sb = new System.Text.StringBuilder();
            sb.Append("# Last download of the online calendar(s), in local time. Rewritten by the Portal.\r\n");
            foreach (CalendarEvent e in events) {
                sb.Append(e.Year.ToString()).Append("-").Append(Two(e.Month)).Append("-").Append(Two(e.Day));
                if (e.Hour >= 0) sb.Append(" ").Append(e.Hour.ToString()).Append(":").Append(Two(e.Minute));
                sb.Append(" ").Append(e.Text).Append("\r\n");
            }
            return sb.ToString();
        }

        static string Two(int v) => v < 10 ? "0" + v : v.ToString();

        static CalendarEvent ParseLine(string line) {
            var e = new CalendarEvent();
            int pos;
            int y, m, d;
            if (line.Length >= 10 && line[4] == '-' && line[7] == '-' &&
                Clock.Digits(line, 0, 4, out y) && Clock.Digits(line, 5, 2, out m) && Clock.Digits(line, 8, 2, out d)) {
                e.Year = y; e.Month = m; e.Day = d;
                pos = 10;
            }
            else if (line.Length >= 5 && line[2] == '-' && Clock.Digits(line, 0, 2, out m) && Clock.Digits(line, 3, 2, out d)) {
                e.Month = m; e.Day = d;
                pos = 5;
            }
            else {
                return null;
            }
            if (e.Month < 1 || e.Month > 12 || e.Day < 1 || e.Day > 31) return null;

            var rest = line.Substring(pos).Trim();
            int hour, minute;
            // Optional start time: H:MM or HH:MM
            var colon = rest.IndexOf(':');
            if (colon == 1 || colon == 2) {
                if (Clock.Digits(rest, 0, colon, out hour) && Clock.Digits(rest, colon + 1, 2, out minute) && hour < 24 && minute < 60) {
                    e.Hour = hour;
                    e.Minute = minute;
                    rest = rest.Substring(colon + 3).Trim();
                }
            }
            e.Text = rest.Length > 0 ? rest : "(no title)";
            return e;
        }

        /// <summary>Events on a day, all-day events first, then by start time.</summary>
        public static ArrayList On(ArrayList events, int year, int month, int day) {
            var list = new ArrayList();
            foreach (CalendarEvent e in events) {
                if (!e.IsOn(year, month, day)) continue;
                var key = e.Hour * 60 + e.Minute;
                var i = 0;
                while (i < list.Count && ((CalendarEvent)list[i]).Hour * 60 + ((CalendarEvent)list[i]).Minute <= key) i++;
                list.Insert(i, e);
            }
            return list;
        }

        public static bool Any(ArrayList events, int year, int month, int day) {
            foreach (CalendarEvent e in events) {
                if (e.IsOn(year, month, day)) return true;
            }
            return false;
        }
    }
}
