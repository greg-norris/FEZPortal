namespace PortalDesktop.Data {
    /// <summary>
    /// The settings files the Portal keeps in the Portal folder on the SD card. When a file is
    /// missing, the Portal writes it with this content, so a blank card gets editable files.
    /// The same files are in the project's SdCard\Portal folder to copy to a card by hand.
    /// </summary>
    public static class DefaultFiles {
        public const string Folder = "Portal";

        public const string WifiName = "wifi.txt";
        public const string WifiHeader =
            "# Wi-Fi networks the Portal can join, most recently used first (up to 8).\r\n" +
            "# You can also pick one on the Portal: tap the Wi-Fi icon at the top right.\r\n" +
            "# More by hand: ssid2=Name and password2=secret, then ssid3/password3 and so on.\r\n";
        public const string WifiAutoJoinComment =
            "# yes: when the Wi-Fi is lost, join whichever saved network is in range.\r\n" +
            "# no: only ever join the first network above.\r\n";
        public const string Wifi =
            WifiHeader +
            "ssid=Hacked\r\n" +
            "password=hackme123\r\n" +
            WifiAutoJoinComment +
            "autojoin=yes\r\n";

        public const string DisplayName = "display.txt";
        public const string Display =
            "# Look of the screen: light (light blue background) or dark (near-black background).\r\n" +
            "# You can also switch it on the Portal: tap \"FEZ Portal Desktop\" at the top left.\r\n" +
            "theme=light\r\n";

        public const string ClockName = "clock.txt";
        public const string Clock =
            "# Time zone: auto uses the time zone of your location (with daylight saving time).\r\n" +
            "# Or give a fixed offset from UTC in hours, for example utcoffset=-5\r\n" +
            "utcoffset=auto\r\n" +
            "# 12 or 24 hour clock (you can also switch it on the Clock screen).\r\n" +
            "format=12\r\n" +
            "# Internet time server.\r\n" +
            "ntpserver=pool.ntp.org\r\n" +
            "# Clock style on the Clock page and the full-screen clock: analog, red, green or blue\r\n" +
            "# (7-segment digital). Tap the clock on the Clock page to change it.\r\n" +
            "face=analog\r\n" +
            "# Countdown length in seconds (set on the Countdown page).\r\n" +
            "countdown=300\r\n" +
            "# Alarm (set on the Alarm page): time, on/off, and repeat = daily, weekdays or once.\r\n" +
            "alarm=7:00\r\n" +
            "alarmon=no\r\n" +
            "alarmrepeat=daily\r\n";

        public const string WeatherName = "weather.txt";
        public const string Weather =
            "# Weather from open-meteo.com (no account needed).\r\n" +
            "# location=auto finds your area from your internet address.\r\n" +
            "# To set it yourself, use a name plus latitude and longitude:\r\n" +
            "#   location=Detroit, MI\r\n" +
            "#   latitude=42.33\r\n" +
            "#   longitude=-83.05\r\n" +
            "location=auto\r\n" +
            "# f for Fahrenheit and mph, c for Celsius and km/h.\r\n" +
            "units=f\r\n";

        public const string StocksName = "stocks.txt";
        public const string Stocks =
            "# Stock quotes from finnhub.io. Lines starting with # are ignored.\r\n" +
            "\r\n" +
            "# Your Finnhub API key (https://finnhub.io/dashboard)\r\n" +
            "apikey=cat09eiad3icrc16c4l0\r\n" +
            "\r\n" +
            "# The watchlist, in display order, separated by commas.\r\n" +
            "# 8 fit on a screen; more make extra pages (swipe left/right). Up to 16.\r\n" +
            "# On the Portal: + at the top of the Stocks page adds one, Remove on a stock's page takes it off.\r\n" +
            "symbols=OKLO,NCLH,NVDA,F,GOOGL,AMC,PFE,GNS\r\n";

        public const string CalendarName = "calendar.txt";
        public const string Calendar =
            "# Online calendars, shown together with the events below: one line per calendar,\r\n" +
            "#   ics=https://outlook.office365.com/owa/calendar/.../calendar.ics\r\n" +
            "# Outlook: Settings > Calendar > Shared calendars > Publish a calendar, then copy the ICS link.\r\n" +
            "# Google and iCloud \"secret address\" (.ics) links work too. Anyone with a link can read that calendar.\r\n" +
            "#\r\n" +
            "# Calendar events, one per line:\r\n" +
            "#   2026-11-20 12:30 Team lunch     one day, with a start time\r\n" +
            "#   2026-11-26 Thanksgiving         one day, all day\r\n" +
            "#   12-25 Christmas Day             every year\r\n" +
            "01-01 New Year's Day\r\n" +
            "02-14 Valentine's Day\r\n" +
            "07-04 Independence Day\r\n" +
            "10-31 Halloween\r\n" +
            "12-24 Christmas Eve\r\n" +
            "12-25 Christmas Day\r\n" +
            "12-31 New Year's Eve\r\n";

        public static readonly string[] Names = { WifiName, DisplayName, ClockName, WeatherName, StocksName, CalendarName };
        public static readonly string[] Contents = { Wifi, Display, Clock, Weather, Stocks, Calendar };
    }
}
