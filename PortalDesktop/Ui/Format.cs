using System;

namespace PortalDesktop.Ui {
    /// <summary>
    /// Text formatting done by hand, so it behaves the same on the board and on the PC
    /// (no culture settings or format strings involved).
    /// </summary>
    public static class Format {
        static readonly string[] Months = {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December" };
        static readonly string[] Days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public static string MonthName(int month) => Months[month - 1];
        public static string ShortMonth(int month) => Months[month - 1].Substring(0, 3);
        public static string DayName(DayOfWeek day) => Days[(int)day];
        public static string ShortDay(DayOfWeek day) => Days[(int)day].Substring(0, 3);

        public static string Two(int v) => v < 10 ? "0" + v : v.ToString();

        /// <summary>"10:42" (12-hour, no AM/PM) or "22:42".</summary>
        public static string Time(DateTime t, bool use24h) {
            if (use24h) return Two(t.Hour) + ":" + Two(t.Minute);
            var h = t.Hour % 12;
            if (h == 0) h = 12;
            return h + ":" + Two(t.Minute);
        }

        public static string TimeWithSeconds(DateTime t, bool use24h) => Time(t, use24h) + ":" + Two(t.Second);

        public static string AmPm(DateTime t) => t.Hour < 12 ? "AM" : "PM";

        /// <summary>"10:42 AM" or "22:42".</summary>
        public static string ShortTime(DateTime t, bool use24h) => use24h ? Time(t, true) : Time(t, false) + " " + AmPm(t);

        /// <summary>"Monday, November 16".</summary>
        public static string LongDate(DateTime t) => DayName(t.DayOfWeek) + ", " + MonthName(t.Month) + " " + t.Day;

        /// <summary>"Mon, Nov 16".</summary>
        public static string ShortDate(DateTime t) => ShortDay(t.DayOfWeek) + ", " + ShortMonth(t.Month) + " " + t.Day;

        /// <summary>"UTC-05:00".</summary>
        public static string UtcOffset(int seconds) {
            var sign = seconds < 0 ? "-" : "+";
            if (seconds < 0) seconds = -seconds;
            return "UTC" + sign + Two(seconds / 3600) + ":" + Two(seconds / 60 % 60);
        }

        /// <summary>Fixed-point number with thousands separators: 1234.5 -> "1,234.50".</summary>
        public static string Number(double value, int decimals) => Number(value, decimals, true);

        /// <summary>Plain fixed-point number for URLs and files: -83.1049 -> "-83.1049".</summary>
        public static string Plain(double value, int decimals) => Number(value, decimals, false);

        static string Number(double value, int decimals, bool group) {
            var negative = value < 0;
            if (negative) value = -value;
            long scale = 1;
            for (var i = 0; i < decimals; i++) scale *= 10;
            var scaled = (long)(value * scale + 0.5);
            var whole = scaled / scale;
            var frac = scaled % scale;

            var digits = whole.ToString();
            var grouped = "";
            for (var i = 0; i < digits.Length; i++) {
                if (group && i > 0 && (digits.Length - i) % 3 == 0) grouped += ",";
                grouped += digits[i];
            }
            if (decimals > 0) {
                var f = frac.ToString();
                while (f.Length < decimals) f = "0" + f;
                grouped += "." + f;
            }
            return (negative && scaled != 0 ? "-" : "") + grouped;
        }

        /// <summary>Always shows the sign: "+1.23" / "-0.50".</summary>
        public static string Signed(double value, int decimals) {
            var s = Number(value, decimals);
            return s[0] == '-' ? s : "+" + s;
        }

        /// <summary>Prices under $1 get more decimals.</summary>
        public static string Price(double value) => Number(value, value < 1 && value > -1 ? 4 : 2);

        public static string Percent(double value) => Signed(value, 2) + "%";

        /// <summary>"$162.04".</summary>
        public static string Money(double value) => "$" + Price(value);

        /// <summary>Market value from millions of dollars: "$850M", "$4.4B", "$3.92T".</summary>
        public static string MarketCap(double millions) {
            if (millions >= 1000000) return "$" + Number(millions / 1000000, 2) + "T";
            if (millions >= 1000) return "$" + Number(millions / 1000, millions < 100000 ? 1 : 0) + "B";
            return "$" + Number(millions, millions < 100 ? 1 : 0) + "M";
        }

        /// <summary>Fixed "HH:MM:SS" (the countdown display).</summary>
        public static string Hms(long seconds) => Two((int)(seconds / 3600)) + ":" + Two((int)(seconds / 60 % 60)) + ":" + Two((int)(seconds % 60));

        /// <summary>Short time left or taken: "4:59", "1:05:00".</summary>
        public static string Span(long seconds) {
            var h = seconds / 3600;
            var m = (int)(seconds / 60 % 60);
            var s = (int)(seconds % 60);
            return h > 0 ? h + ":" + Two(m) + ":" + Two(s) : m + ":" + Two(s);
        }

        /// <summary>Stopwatch time with tenths: "00:12.3", "1:02:03.4".</summary>
        public static string Stopwatch(long ms) {
            var tenths = (int)(ms / 100 % 10);
            var s = ms / 1000;
            var h = s / 3600;
            var text = h > 0 ? h + ":" + Two((int)(s / 60 % 60)) : Two((int)(s / 60 % 60));
            return text + ":" + Two((int)(s % 60)) + "." + tenths;
        }

        /// <summary>A length in words: "45 sec", "5 min", "1 h 30 min".</summary>
        public static string Length(int seconds) {
            if (seconds < 60) return seconds + " sec";
            var h = seconds / 3600;
            var m = seconds / 60 % 60;
            var s = seconds % 60;
            var text = h > 0 ? h + " h" : "";
            if (m > 0) text += (text.Length > 0 ? " " : "") + m + " min";
            if (s > 0) text += (text.Length > 0 ? " " : "") + s + " sec";
            return text;
        }

        /// <summary>Whole degrees: 39.6 -> "40°".</summary>
        public static string Degrees(double value) => Round(value) + "°";

        public static int Round(double value) => (int)(value < 0 ? value - 0.5 : value + 0.5);
    }
}
