using System;
using System.Collections;
using System.Text;

namespace PortalDesktop.Data {
    /// <summary>Receives a text download one line at a time (see Http.GetLines).</summary>
    public abstract class LineSink {
        /// <summary>
        /// While true, folded continuation lines (starting with a space) are dropped without being
        /// decoded: the reader sets it while it is inside a property it doesn't keep.
        /// </summary>
        public bool SkipFolded;

        public abstract void Line(string line);
    }

    /// <summary>A time zone from a VTIMEZONE block: standard and daylight offsets and when they switch.</summary>
    public class IcsZone {
        public string Id = "";
        public int StdOffset, DstOffset;            // seconds east of UTC
        public bool HasStd, HasDst;
        // Switch rules: month, week of the month (1..4, -1 = last), day of week, minutes after midnight (local).
        public int StdMonth, StdWeek, StdDow, StdMinute;
        public int DstMonth, DstWeek, DstDow, DstMinute;

        /// <summary>Offset in effect at a wall-clock time in this zone.</summary>
        public int OffsetAtWall(DateTime wall) => this.IsDaylight(wall) ? this.DstOffset : this.StdOffset;

        /// <summary>Offset in effect at a UTC instant.</summary>
        public int OffsetAtUtc(DateTime utc) => this.IsDaylight(utc.AddSeconds(this.StdOffset)) ? this.DstOffset : this.StdOffset;

        public DateTime ToUtc(DateTime wall) => wall.AddSeconds(-this.OffsetAtWall(wall));

        bool IsDaylight(DateTime wall) {
            if (!this.HasDst || !this.HasStd || this.DstMonth == 0 || this.StdMonth == 0) return false;
            var start = Ics.NthWeekday(wall.Year, this.DstMonth, this.DstWeek, this.DstDow).AddMinutes(this.DstMinute);
            var end = Ics.NthWeekday(wall.Year, this.StdMonth, this.StdWeek, this.StdDow).AddMinutes(this.StdMinute);
            return start < end ? wall >= start && wall < end : wall >= start || wall < end;   // southern hemisphere wraps
        }
    }

    /// <summary>One VEVENT as read (before repeats are worked out).</summary>
    class IcsEvent {
        public string Uid = "", Summary = "", Location = "", Description = "";
        public DateTime Start, End;
        public bool HasEnd;
        public IcsZone Zone;            // null: UTC (Utc = true) or floating (Portal local time)
        public bool Utc, AllDay, Cancelled;
        public string Rrule;
        public ArrayList Exdates;       // of DateTime (UTC for timed events, dates for all-day ones)
        public DateTime RecurrenceId = DateTime.MinValue;   // UTC (or date) of the instance this one replaces
    }

    /// <summary>
    /// Reads an iCalendar (.ics) feed such as Outlook's published calendar, streamed line by line
    /// so the long meeting descriptions never sit in memory, then expands it into CalendarEvents
    /// in the Portal's local time. Supports VTIMEZONE (with daylight saving), all-day and multi-day
    /// events, RRULE (DAILY/WEEKLY/MONTHLY/YEARLY with INTERVAL, COUNT, UNTIL, BYDAY, BYMONTHDAY,
    /// BYMONTH, BYSETPOS), EXDATE, moved/edited instances (RECURRENCE-ID) and cancelled events.
    /// </summary>
    public class Ics : LineSink {
        const int MaxInstances = 1500;
        public const int MaxDescription = 2000;   // characters of an event's message kept for its page
        const int MaxSteps = 5000;

        readonly ArrayList zones = new ArrayList();
        readonly ArrayList events = new ArrayList();
        string pending;                 // the logical line being unfolded
        IcsEvent current;
        IcsZone zone;
        int zonePart;                   // 0 none, 1 STANDARD, 2 DAYLIGHT
        int partOffset, partMonth, partWeek, partDow, partMinute;
        int alarmDepth;                 // inside VALARM (ignored)

        public int EventCount => this.events.Count;

        // -- Reading ---------------------------------------------------------------------

        public override void Line(string line) {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t')) {
                if (this.pending == null) return;
                if (this.pending.Length < MaxDescription + 1000) this.pending += line.Substring(1);
                else this.SkipFolded = true;    // a very long message: the rest isn't needed
                return;
            }
            this.Flush();
            var name = NameOf(line);
            if (Kept(name)) {
                this.pending = line;
                this.SkipFolded = false;
            }
            else {
                this.pending = null;
                this.SkipFolded = true;     // e.g. DESCRIPTION: don't even decode its continuation lines
            }
        }

        /// <summary>Call after the last line.</summary>
        public void Finish() => this.Flush();

        static bool Kept(string name) =>
            name == "BEGIN" || name == "END" || name == "UID" || name == "SUMMARY" || name == "DTSTART" || name == "DTEND" ||
            name == "RRULE" || name == "EXDATE" || name == "RECURRENCE-ID" || name == "STATUS" || name == "TZID" ||
            name == "TZOFFSETTO" || name == "X-MICROSOFT-CDO-ALLDAYEVENT" || name == "LOCATION" || name == "DESCRIPTION";

        static string NameOf(string line) {
            for (var i = 0; i < line.Length; i++) {
                if (line[i] == ';' || line[i] == ':') return line.Substring(0, i).ToUpper();
            }
            return line.ToUpper();
        }

        void Flush() {
            var line = this.pending;
            this.pending = null;
            if (line == null) return;

            // NAME;PARAM=..;PARAM="..":VALUE (a ':' inside quotes belongs to a parameter)
            var colon = -1;
            var quoted = false;
            for (var i = 0; i < line.Length; i++) {
                if (line[i] == '"') quoted = !quoted;
                else if (line[i] == ':' && !quoted) { colon = i; break; }
            }
            if (colon < 0) return;
            var head = line.Substring(0, colon);
            var value = line.Substring(colon + 1);
            var semi = head.IndexOf(';');
            var name = (semi < 0 ? head : head.Substring(0, semi)).ToUpper();
            var parameters = semi < 0 ? "" : head.Substring(semi + 1);
            this.Property(name, parameters, value);
        }

        void Property(string name, string parameters, string value) {
            if (name == "BEGIN") {
                var what = value.Trim().ToUpper();
                if (what == "VALARM") this.alarmDepth++;
                else if (what == "VEVENT") this.current = new IcsEvent();
                else if (what == "VTIMEZONE") this.zone = new IcsZone();
                else if (what == "STANDARD" || what == "DAYLIGHT") {
                    this.zonePart = what == "STANDARD" ? 1 : 2;
                    this.partOffset = this.partMonth = this.partWeek = this.partDow = this.partMinute = 0;
                }
                return;
            }
            if (name == "END") {
                var what = value.Trim().ToUpper();
                if (what == "VALARM") { if (this.alarmDepth > 0) this.alarmDepth--; }
                else if (what == "VEVENT") {
                    if (this.current != null && this.current.Start != DateTime.MinValue) this.events.Add(this.current);
                    this.current = null;
                }
                else if (what == "VTIMEZONE") {
                    if (this.zone != null && this.zone.Id.Length > 0) this.zones.Add(this.zone);
                    this.zone = null;
                }
                else if ((what == "STANDARD" || what == "DAYLIGHT") && this.zone != null) this.EndZonePart();
                return;
            }
            if (this.alarmDepth > 0) return;

            if (this.zone != null && this.current == null) {
                if (name == "TZID") this.zone.Id = value.Trim();
                else if (name == "TZOFFSETTO") this.partOffset = ParseOffset(value);
                else if (name == "DTSTART" && value.Length >= 15) {
                    int h, m;
                    if (Clock.Digits(value, 9, 2, out h) && Clock.Digits(value, 11, 2, out m)) this.partMinute = h * 60 + m;
                }
                else if (name == "RRULE") this.ZoneRule(value);
                return;
            }

            var e = this.current;
            if (e == null) return;
            switch (name) {
                case "UID": e.Uid = value; break;
                case "SUMMARY": e.Summary = Unescape(value); break;
                case "LOCATION": e.Location = Unescape(value); break;
                case "DESCRIPTION": e.Description = CleanMessage(value); break;
                case "STATUS": e.Cancelled = value.Trim().ToUpper() == "CANCELLED"; break;
                case "RRULE": e.Rrule = value.ToUpper(); break;
                case "X-MICROSOFT-CDO-ALLDAYEVENT": if (value.Trim().ToUpper() == "TRUE") e.AllDay = true; break;
                case "DTSTART": {
                    bool utc, dateOnly;
                    if (!ParseDate(value, out e.Start, out utc, out dateOnly)) return;
                    e.Utc = utc;
                    e.AllDay |= dateOnly;
                    e.Zone = utc ? null : this.FindZone(Param(parameters, "TZID"));
                    break;
                }
                case "DTEND": {
                    bool utc, dateOnly;
                    if (ParseDate(value, out e.End, out utc, out dateOnly)) e.HasEnd = true;
                    break;
                }
                case "RECURRENCE-ID":
                    e.RecurrenceId = this.ToUtcOrDate(value, parameters);
                    break;
                case "EXDATE":
                    if (e.Exdates == null) e.Exdates = new ArrayList();
                    foreach (var part in value.Split(',')) {
                        var t = this.ToUtcOrDate(part, parameters);
                        if (t != DateTime.MinValue) e.Exdates.Add(t);
                    }
                    break;
            }
        }

        void EndZonePart() {
            if (this.zonePart == 1) {
                this.zone.HasStd = true;
                this.zone.StdOffset = this.partOffset;
                this.zone.StdMonth = this.partMonth; this.zone.StdWeek = this.partWeek; this.zone.StdDow = this.partDow; this.zone.StdMinute = this.partMinute;
            }
            else if (this.zonePart == 2) {
                this.zone.HasDst = true;
                this.zone.DstOffset = this.partOffset;
                this.zone.DstMonth = this.partMonth; this.zone.DstWeek = this.partWeek; this.zone.DstDow = this.partDow; this.zone.DstMinute = this.partMinute;
            }
            this.zonePart = 0;
        }

        /// <summary>The yearly switch rule of a STANDARD/DAYLIGHT part, e.g. FREQ=YEARLY;BYDAY=2SU;BYMONTH=3.</summary>
        void ZoneRule(string rule) {
            foreach (var part in rule.ToUpper().Split(';')) {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                var key = part.Substring(0, eq);
                var val = part.Substring(eq + 1);
                if (key == "BYMONTH") this.partMonth = ToInt(val);
                else if (key == "BYDAY") {
                    int n, dow;
                    if (ParseByDay(val.Split(',')[0], out n, out dow)) { this.partWeek = n == 0 ? 1 : n; this.partDow = dow; }
                }
            }
        }

        IcsZone FindZone(string id) {
            if (id == null) return null;
            foreach (IcsZone z in this.zones) {
                if (z.Id == id) return z;
            }
            return null;
        }

        /// <summary>A RECURRENCE-ID or EXDATE value as UTC (timed) or as a date (all-day).</summary>
        DateTime ToUtcOrDate(string value, string parameters) {
            DateTime t;
            bool utc, dateOnly;
            if (!ParseDate(value.Trim(), out t, out utc, out dateOnly)) return DateTime.MinValue;
            if (utc || dateOnly) return t;
            var z = this.FindZone(Param(parameters, "TZID"));
            return z != null ? z.ToUtc(t) : t.AddSeconds(-Clock.OffsetSeconds);
        }

        // -- Expanding ---------------------------------------------------------------------

        /// <summary>
        /// The events (and repeats) that fall between two local dates, as CalendarEvents in the
        /// Portal's local time. localOffset is the Portal's current UTC offset in seconds.
        /// </summary>
        public ArrayList Expand(DateTime fromLocal, DateTime toLocal, DateTime nowUtc, int localOffset) {
            var result = new ArrayList();
            var local = this.LocalZone(nowUtc, localOffset);

            // Instances replaced by an edited copy (same UID + RECURRENCE-ID).
            var replaced = new ArrayList();
            foreach (IcsEvent e in this.events) {
                if (e.RecurrenceId != DateTime.MinValue) replaced.Add(e.Uid + "|" + e.RecurrenceId.Ticks);
            }

            var walls = new ArrayList();
            foreach (IcsEvent e in this.events) {
                if (result.Count >= MaxInstances) break;
                if (e.Cancelled) continue;
                walls.Clear();
                if (e.Rrule != null && e.RecurrenceId == DateTime.MinValue) this.Repeats(e, toLocal.AddDays(1), walls);
                else walls.Add(e.Start);

                foreach (DateTime wall in walls) {
                    if (e.AllDay) {
                        var day = new DateTime(wall.Year, wall.Month, wall.Day);
                        if (e.Rrule != null && (Contains(replaced, e.Uid + "|" + day.Ticks) || Contains(e.Exdates, day))) continue;
                        var days = e.HasEnd ? (int)((e.End - e.Start).Ticks / TimeSpan.TicksPerDay) : 1;
                        if (days < 1) days = 1;
                        if (days > 62) days = 62;
                        for (var d = 0; d < days; d++) Add(result, day.AddDays(d), -1, 0, e, DateTime.MinValue, fromLocal, toLocal);
                        continue;
                    }
                    var utc = this.WallToUtc(e, wall);
                    if (e.Rrule != null && (Contains(replaced, e.Uid + "|" + utc.Ticks) || Contains(e.Exdates, utc))) continue;
                    var t = local != null ? utc.AddSeconds(local.OffsetAtUtc(utc)) : utc.AddSeconds(localOffset);
                    Add(result, t, t.Hour, t.Minute, e, e.HasEnd ? t.Add(e.End - e.Start) : DateTime.MinValue, fromLocal, toLocal);
                }
            }
            return result;
        }

        /// <summary>The feed's zone that matches the Portal's current offset (so daylight saving moves with it), if any.</summary>
        IcsZone LocalZone(DateTime nowUtc, int localOffset) {
            foreach (IcsZone z in this.zones) {
                if (z.OffsetAtUtc(nowUtc) == localOffset) return z;
            }
            return null;
        }

        DateTime WallToUtc(IcsEvent e, DateTime wall) {
            if (e.Utc) return wall;
            if (e.Zone != null) return e.Zone.ToUtc(wall);
            return wall.AddSeconds(-Clock.OffsetSeconds);   // floating time: as on the wall here
        }

        static void Add(ArrayList list, DateTime t, int hour, int minute, IcsEvent e, DateTime end, DateTime from, DateTime to) {
            if (t < from || t >= to.AddDays(1) || list.Count >= MaxInstances) return;
            list.Add(new CalendarEvent {
                Year = t.Year, Month = t.Month, Day = t.Day, Hour = hour, Minute = minute,
                Text = e.Summary.Length > 0 ? e.Summary : "(no title)",
                EndHour = end != DateTime.MinValue && end > t ? end.Hour : -1, EndMinute = end.Minute,
                Location = e.Location, Description = e.Description,   // shared by all repeats (not copied)
            });
        }

        static bool Contains(ArrayList list, string s) {
            foreach (string x in list) {
                if (x == s) return true;
            }
            return false;
        }

        static bool Contains(ArrayList list, DateTime t) {
            if (list == null) return false;
            foreach (DateTime x in list) {
                if (x == t) return true;
            }
            return false;
        }

        /// <summary>Start times (wall clock, the event's zone) of a repeating event, up to limit.</summary>
        void Repeats(IcsEvent e, DateTime limit, ArrayList walls) {
            string freq = "", byDay = null;
            int interval = 1, count = 0, byMonthDay = 0, byMonth = 0, setPos = 0, weekStart = (int)DayOfWeek.Monday;
            var until = DateTime.MaxValue;
            var untilUtc = false;
            foreach (var part in e.Rrule.Split(';')) {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                var key = part.Substring(0, eq);
                var val = part.Substring(eq + 1);
                if (key == "FREQ") freq = val;
                else if (key == "INTERVAL") interval = Math.Max(1, ToInt(val));
                else if (key == "COUNT") count = ToInt(val);
                else if (key == "BYDAY") byDay = val;
                else if (key == "BYMONTHDAY") byMonthDay = ToInt(val.Split(',')[0]);
                else if (key == "BYMONTH") byMonth = ToInt(val.Split(',')[0]);
                else if (key == "BYSETPOS") setPos = ToInt(val.Split(',')[0]);
                else if (key == "WKST") { int n; ParseByDay(val, out n, out weekStart); }
                else if (key == "UNTIL") { bool dateOnly; ParseDate(val, out until, out untilUtc, out dateOnly); if (dateOnly) until = until.AddDays(1).AddTicks(-1); }
            }

            var start = e.Start;
            var time = start.TimeOfDay;
            var emitted = 0;

            if (freq == "WEEKLY") {
                // Days of the week, in week order starting at WKST.
                var days = new int[7];
                var dayCount = 0;
                if (byDay != null) {
                    foreach (var d in byDay.Split(',')) {
                        int n, dow;
                        if (ParseByDay(d, out n, out dow)) days[dayCount++] = dow;
                    }
                }
                if (dayCount == 0) days[dayCount++] = (int)start.DayOfWeek;
                SortByWeek(days, dayCount, weekStart);

                var weekFirst = start.Date.AddDays(-(((int)start.DayOfWeek - weekStart + 7) % 7));
                for (var step = 0; step < MaxSteps; step++) {
                    var week = weekFirst.AddDays(7 * interval * step);
                    for (var i = 0; i < dayCount; i++) {
                        var t = week.AddDays((days[i] - weekStart + 7) % 7).Add(time);
                        if (t < start) continue;
                        if (!this.Emit(e, t, limit, until, untilUtc, count, ref emitted, walls)) return;
                    }
                }
                return;
            }

            for (var step = 0; step < MaxSteps; step++) {
                DateTime t;
                if (freq == "DAILY") {
                    t = start.AddDays(interval * step);
                }
                else if (freq == "MONTHLY" || freq == "YEARLY") {
                    var months = freq == "MONTHLY" ? interval * step : 12 * interval * step;
                    var monthIndex = start.Month - 1 + months;   // no DateTime.AddMonths on TinyCLR
                    var first = new DateTime(start.Year + monthIndex / 12, monthIndex % 12 + 1, 1);
                    if (freq == "YEARLY" && byMonth > 0) first = new DateTime(first.Year, byMonth, 1);
                    if (!DayInMonth(first, start, byDay, byMonthDay, setPos, out t)) continue;   // e.g. the 31st in a short month
                    t = t.Add(time);
                    if (t < start) continue;
                }
                else {
                    walls.Add(start);   // unknown rule: just the first one
                    return;
                }
                if (!this.Emit(e, t, limit, until, untilUtc, count, ref emitted, walls)) return;
            }
        }

        /// <summary>Adds one repeat; false when the rule (UNTIL, COUNT) or the window says stop.</summary>
        bool Emit(IcsEvent e, DateTime wall, DateTime limit, DateTime until, bool untilUtc, int count, ref int emitted, ArrayList walls) {
            if (wall > limit) return false;
            if (until != DateTime.MaxValue && (untilUtc ? this.WallToUtc(e, wall) : wall) > until) return false;
            if (count > 0 && emitted >= count) return false;
            emitted++;
            walls.Add(wall);
            return walls.Count < MaxInstances;
        }

        /// <summary>The day in the month starting at first: BYMONTHDAY, BYDAY with a position (2TU, -1FR, or BYSETPOS), or the start's day.</summary>
        static bool DayInMonth(DateTime first, DateTime start, string byDay, int byMonthDay, int setPos, out DateTime day) {
            var length = DateTime.DaysInMonth(first.Year, first.Month);
            day = DateTime.MinValue;
            if (byMonthDay != 0) {
                var d = byMonthDay > 0 ? byMonthDay : length + byMonthDay + 1;
                if (d < 1 || d > length) return false;
                day = first.AddDays(d - 1);
                return true;
            }
            if (byDay != null) {
                int n, dow;
                if (!ParseByDay(byDay.Split(',')[0], out n, out dow)) return false;
                if (n == 0) n = setPos != 0 ? setPos : 1;
                day = NthWeekday(first.Year, first.Month, n, dow);
                return day.Month == first.Month;
            }
            if (start.Day > length) return false;
            day = first.AddDays(start.Day - 1);
            return true;
        }

        static void SortByWeek(int[] days, int count, int weekStart) {
            for (var a = 1; a < count; a++) {
                var v = days[a];
                var b = a - 1;
                while (b >= 0 && (days[b] - weekStart + 7) % 7 > (v - weekStart + 7) % 7) { days[b + 1] = days[b]; b--; }
                days[b + 1] = v;
            }
        }

        // -- Small parsers ---------------------------------------------------------------------

        /// <summary>The n-th (1..5, or -1 = last) given weekday of a month.</summary>
        public static DateTime NthWeekday(int year, int month, int n, int dow) {
            if (n < 0) {
                var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                return last.AddDays(-(((int)last.DayOfWeek - dow + 7) % 7));
            }
            var first = new DateTime(year, month, 1);
            return first.AddDays((dow - (int)first.DayOfWeek + 7) % 7 + 7 * (n - 1));
        }

        /// <summary>"2SU" -> n = 2, Sunday; "-1FR" -> n = -1, Friday; "MO" -> n = 0, Monday.</summary>
        static bool ParseByDay(string s, out int n, out int dow) {
            n = 0;
            dow = 0;
            s = s.Trim();
            if (s.Length < 2) return false;
            var code = s.Substring(s.Length - 2);
            var codes = "SUMOTUWETHFRSA";
            var at = codes.IndexOf(code);
            if (at < 0 || at % 2 != 0) return false;
            dow = at / 2;
            if (s.Length > 2) n = ToInt(s.Substring(0, s.Length - 2));
            return true;
        }

        /// <summary>"20261116", "20261116T093000" or "20261116T143000Z".</summary>
        static bool ParseDate(string s, out DateTime t, out bool utc, out bool dateOnly) {
            t = DateTime.MinValue;
            utc = false;
            dateOnly = false;
            s = s.Trim();
            int y, mo, d, h = 0, mi = 0, se = 0;
            if (s.Length < 8 || !Clock.Digits(s, 0, 4, out y) || !Clock.Digits(s, 4, 2, out mo) || !Clock.Digits(s, 6, 2, out d)) return false;
            if (mo < 1 || mo > 12 || d < 1 || d > DateTime.DaysInMonth(y, mo)) return false;
            if (s.Length >= 15 && s[8] == 'T') {
                if (!Clock.Digits(s, 9, 2, out h) || !Clock.Digits(s, 11, 2, out mi) || !Clock.Digits(s, 13, 2, out se) || h > 23 || mi > 59 || se > 59) return false;
                utc = s.Length > 15 && s[15] == 'Z';
            }
            else {
                dateOnly = true;
            }
            t = new DateTime(y, mo, d, h, mi, se);
            return true;
        }

        /// <summary>"-0500" -> -18000 seconds.</summary>
        static int ParseOffset(string s) {
            s = s.Trim();
            if (s.Length < 5) return 0;
            int h, m;
            if (!Clock.Digits(s, 1, 2, out h) || !Clock.Digits(s, 3, 2, out m)) return 0;
            var seconds = h * 3600 + m * 60;
            return s[0] == '-' ? -seconds : seconds;
        }

        static string Param(string parameters, string name) {
            foreach (var p in parameters.Split(';')) {
                var eq = p.IndexOf('=');
                if (eq > 0 && p.Substring(0, eq).ToUpper() == name) {
                    var v = p.Substring(eq + 1);
                    if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"') v = v.Substring(1, v.Length - 2);
                    return v;
                }
            }
            return null;
        }

        static int ToInt(string s) {
            s = s.Trim();
            var negative = s.Length > 0 && s[0] == '-';
            var start = s.Length > 0 && (s[0] == '-' || s[0] == '+') ? 1 : 0;
            var v = 0;
            for (var i = start; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++) v = v * 10 + (s[i] - '0');
            return negative ? -v : v;
        }

        /// <summary>
        /// A DESCRIPTION made readable on the small screen: escapes undone (keeping line breaks),
        /// Outlook's hidden link addresses ("Join<https://...>") removed, other long addresses
        /// shortened to their site, separator lines and runs of blank lines dropped, and capped.
        /// </summary>
        static string CleanMessage(string value) {
            // Unescape, keeping \n as a line break.
            var sb = new StringBuilder();
            for (var i = 0; i < value.Length; i++) {
                var c = value[i];
                if (c == '\\' && i + 1 < value.Length) {
                    var n = value[++i];
                    sb.Append(n == 'n' || n == 'N' ? '\n' : n);
                }
                else if (c != '\r') {
                    sb.Append(c);
                }
            }
            var text = sb.ToString();

            var result = new StringBuilder();
            var blank = 0;
            foreach (var raw in text.Split('\n')) {
                var line = ShortenLinks(raw).Trim();
                if (IsSeparator(line)) line = "";
                if (line.Length == 0) {
                    if (result.Length > 0) blank++;
                    continue;
                }
                if (result.Length > 0) result.Append(blank > 0 ? "\n\n" : "\n");
                blank = 0;
                result.Append(line);
                if (result.Length >= MaxDescription) break;
            }
            var cleaned = result.ToString();
            return cleaned.Length > MaxDescription ? cleaned.Substring(0, MaxDescription) + "..." : cleaned;
        }

        /// <summary>Drops "<https://...>" (link addresses after their text) and shortens long bare addresses.</summary>
        static string ShortenLinks(string line) {
            var sb = new StringBuilder();
            var i = 0;
            while (i < line.Length) {
                if (line[i] == '<' && Starts(line, i + 1, "http")) {
                    var close = line.IndexOf('>', i);
                    if (close > i) { i = close + 1; continue; }
                }
                if (Starts(line, i, "http://") || Starts(line, i, "https://")) {
                    var end = i;
                    while (end < line.Length && line[end] != ' ' && line[end] != '>' && line[end] != ')') end++;
                    var url = line.Substring(i, end - i);
                    if (url.Length > 40) {
                        var hostStart = url.IndexOf("//") + 2;
                        var hostEnd = url.IndexOf('/', hostStart);
                        url = (hostEnd > 0 ? url.Substring(hostStart, hostEnd - hostStart) : url.Substring(hostStart)) + "/...";
                    }
                    sb.Append(url);
                    i = end;
                    continue;
                }
                sb.Append(line[i]);
                i++;
            }
            return sb.ToString();
        }

        static bool Starts(string s, int at, string prefix) {
            if (at + prefix.Length > s.Length) return false;
            for (var k = 0; k < prefix.Length; k++) {
                var c = s[at + k];
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                if (c != prefix[k]) return false;
            }
            return true;
        }

        /// <summary>A line of only ____, ----, ==== or similar.</summary>
        static bool IsSeparator(string line) {
            if (line.Length < 5) return false;
            for (var i = 0; i < line.Length; i++) {
                var c = line[i];
                if (c != '_' && c != '-' && c != '=' && c != '*' && c != '.' && c != ' ') return false;
            }
            return true;
        }

        /// <summary>Text values: "\," "\;" "\\" and "\n".</summary>
        static string Unescape(string s) {
            if (s.IndexOf('\\') < 0) return s.Trim();
            var sb = new StringBuilder();
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c == '\\' && i + 1 < s.Length) {
                    var n = s[++i];
                    sb.Append(n == 'n' || n == 'N' ? ' ' : n);
                }
                else {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }
    }
}
