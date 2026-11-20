using System;

namespace PortalDesktop.Data {
    /// <summary>
    /// US market hours. The regular session runs 9:30-16:00 New York time, so the stock screens
    /// work in Eastern time (with US daylight saving rules) whatever time zone the Portal is in.
    /// Early-close days are not known; Finnhub's market status still says when it is closed.
    /// </summary>
    public static class Market {
        public const int OpenMinute = 9 * 60 + 30;
        public const int CloseMinute = 16 * 60;
        public const int SessionMinutes = CloseMinute - OpenMinute;

        /// <summary>UTC offset of New York at the given UTC time: -4 h in summer, -5 h in winter.</summary>
        public static int EasternOffsetSeconds(DateTime utc) {
            // Daylight time runs from 2:00 on the second Sunday of March to 2:00 on the first Sunday of November.
            var start = NthSunday(utc.Year, 3, 2).AddHours(2 + 5);
            var end = NthSunday(utc.Year, 11, 1).AddHours(2 + 4);
            return utc >= start && utc < end ? -4 * 3600 : -5 * 3600;
        }

        public static DateTime ToEastern(DateTime utc) => utc.AddSeconds(EasternOffsetSeconds(utc));

        public static DateTime EasternFromUnix(long unixSeconds) => ToEastern(Clock.FromUnix(unixSeconds));

        /// <summary>Minutes since midnight, Eastern time.</summary>
        public static double EasternMinutes(DateTime eastern) => eastern.Hour * 60 + eastern.Minute + eastern.Second / 60.0;

        /// <summary>Where a trade time falls in its day's regular session: 0 = 9:30, 1 = 16:00 (clamped).</summary>
        public static double SessionFraction(long unixSeconds) {
            var f = (EasternMinutes(EasternFromUnix(unixSeconds)) - OpenMinute) / SessionMinutes;
            return f < 0 ? 0 : f > 1 ? 1 : f;
        }

        /// <summary>"Market open · closes in 5h 18m", "Pre-market · opens in 45m", "Market closed"...</summary>
        public static string StatusText(PortalState s, DateTime utc) {
            var minutes = EasternMinutes(ToEastern(utc));
            if (s.MarketOpen) {
                var left = (int)(CloseMinute - minutes);
                return left > 0 ? "Market open · closes in " + Duration(left) : "Market open";
            }
            if (s.MarketSession == "pre-market") {
                var left = (int)(OpenMinute - minutes + 0.999);
                return left > 0 ? "Pre-market · opens in " + Duration(left) : "Pre-market";
            }
            if (s.MarketSession == "post-market") return "After hours";
            if (s.MarketHoliday != null && s.MarketHoliday.Length > 0) return "Closed · " + s.MarketHoliday;
            return "Market closed";
        }

        /// <summary>"6h 09m" or "45m".</summary>
        public static string Duration(int minutes) => minutes >= 60 ? minutes / 60 + "h " + (minutes % 60 < 10 ? "0" : "") + minutes % 60 + "m" : minutes + "m";

        static DateTime NthSunday(int year, int month, int n) {
            var first = new DateTime(year, month, 1);
            var toSunday = (7 - (int)first.DayOfWeek) % 7;
            return first.AddDays(toSunday + 7 * (n - 1));
        }
    }
}
