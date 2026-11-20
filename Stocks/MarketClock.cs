namespace Stocks;

/// <summary>
/// New York time and the regular US trading session (9:30 to 16:00 ET).
/// The board may have no battery-backed clock, so the time is corrected
/// from Finnhub's server time (<see cref="Calibrate"/>).
/// </summary>
static class MarketClock {
    public static readonly TimeSpan SessionOpen = new(9, 30, 0);
    public static readonly TimeSpan SessionClose = new(16, 0, 0);

    private static readonly TimeZoneInfo? Eastern = FindEastern();
    private static long offsetMs;   // server clock minus this board's clock

    /// <summary>The time, corrected by the last <see cref="Calibrate"/>.</summary>
    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow.AddMilliseconds(Interlocked.Read(ref offsetMs));

    public static long UnixNow => UtcNow.ToUnixTimeSeconds();

    /// <summary>Use a server timestamp (Unix seconds) to correct the board's clock.</summary>
    public static void Calibrate(long serverUnix) {
        if (serverUnix <= 0) return;
        var diff = (serverUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) * 1000;
        // Only a clearly wrong clock is corrected; the server time has 1 s resolution.
        Interlocked.Exchange(ref offsetMs, Math.Abs(diff) > 3000 ? diff : 0);
    }

    public static DateTime ToEastern(DateTimeOffset utc) => utc.UtcDateTime + UtcOffset(utc.UtcDateTime);

    public static DateTime ToEastern(long unix) => ToEastern(DateTimeOffset.FromUnixTimeSeconds(unix));

    public static DateTime EasternNow => ToEastern(UtcNow);

    /// <summary>The trading day (New York date) a Unix time belongs to.</summary>
    public static DateOnly SessionDate(long unix) => DateOnly.FromDateTime(ToEastern(unix));

    /// <summary>Unix time of <paramref name="etTime"/> on New York date <paramref name="date"/>.</summary>
    public static long Unix(DateOnly date, TimeSpan etTime) {
        var local = date.ToDateTime(TimeOnly.MinValue) + etTime;
        var utc = local - UtcOffset(local.AddHours(5));   // offset at roughly that moment
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }

    public static (long Open, long Close) Session(DateOnly date) => (Unix(date, SessionOpen), Unix(date, SessionClose));

    private static TimeSpan UtcOffset(DateTime utc) {
        if (Eastern is not null) return Eastern.GetUtcOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

        // No time zone database: US rules since 2007. Daylight time runs from
        // the second Sunday of March, 2:00 EST (7:00 UTC), to the first Sunday
        // of November, 2:00 EDT (6:00 UTC).
        var start = NthSunday(utc.Year, 3, 2).AddHours(7);
        var end = NthSunday(utc.Year, 11, 1).AddHours(6);
        return utc >= start && utc < end ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
    }

    private static DateTime NthSunday(int year, int month, int n) {
        var first = new DateTime(year, month, 1);
        var toSunday = ((int)DayOfWeek.Sunday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(toSunday + 7 * (n - 1));
    }

    private static TimeZoneInfo? FindEastern() {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" }) {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception) { }
        }
        return null;
    }
}
