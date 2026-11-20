using System;

namespace PortalDesktop.Data {
    /// <summary>
    /// Local time. The system clock runs in UTC (set from the RTC and internet time);
    /// the local time adds the offset of the time zone in use.
    /// </summary>
    public static class Clock {
        static int offsetSeconds;
        static DateTime fixedUtc = DateTime.MinValue;

        /// <summary>Seconds to add to UTC for local time (including daylight saving time).</summary>
        public static int OffsetSeconds {
            get => offsetSeconds;
            set => offsetSeconds = value;
        }

        public static DateTime UtcNow => fixedUtc != DateTime.MinValue ? fixedUtc : DateTime.UtcNow;

        public static DateTime Now => UtcNow.AddSeconds(offsetSeconds);

        /// <summary>Freezes the clock (for the desktop preview).</summary>
        public static void Freeze(DateTime utc) => fixedUtc = utc;

        public static DateTime FromUnix(double unixSeconds) => new DateTime(1970, 1, 1).AddSeconds(unixSeconds);

        public static DateTime ToLocal(DateTime utc) => utc.AddSeconds(offsetSeconds);

        /// <summary>Parses "2026-11-16" or "2026-11-16T08:30" (no time zone).</summary>
        public static bool TryParseIso(string text, out DateTime result) {
            result = DateTime.MinValue;
            if (text == null || text.Length < 10) return false;
            int year, month, day, hour = 0, minute = 0;
            if (!Digits(text, 0, 4, out year) || !Digits(text, 5, 2, out month) || !Digits(text, 8, 2, out day)) return false;
            if (text.Length >= 16 && (!Digits(text, 11, 2, out hour) || !Digits(text, 14, 2, out minute))) return false;
            if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59) return false;
            result = new DateTime(year, month, day, hour, minute, 0);
            return true;
        }

        public static bool Digits(string s, int start, int count, out int value) {
            value = 0;
            if (start + count > s.Length) return false;
            for (var i = start; i < start + count; i++) {
                var c = s[i];
                if (c < '0' || c > '9') return false;
                value = value * 10 + (c - '0');
            }
            return true;
        }
    }
}
