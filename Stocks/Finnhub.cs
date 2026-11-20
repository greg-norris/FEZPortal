using System.Net;
using System.Text.Json;

namespace Stocks;

/// <summary>A /quote result. Prices are in the stock's currency.</summary>
sealed record Quote(double Current, double Change, double ChangePercent, double High, double Low,
                    double Open, double PrevClose, long Time) {
    public bool IsUp => this.Change >= 0;
}

/// <summary>Company facts from /stock/profile2 (or /search for funds), plus /stock/metric.</summary>
sealed record Profile(string Name, string Exchange, string Industry, string Currency, double SharesMillions) {
    public double? WeekHigh52 { get; init; }
    public double? WeekLow52 { get; init; }
    public double? PeRatio { get; init; }
    public double? DividendYield { get; init; }
    public double? Beta { get; init; }
}

sealed record MarketStatus(bool IsOpen, string? Session, string? Holiday, long Time);

sealed class FinnhubException : Exception {
    public FinnhubException(HttpStatusCode status, string message) : base(message) => this.Status = status;
    public HttpStatusCode Status { get; }
}

/// <summary>
/// The few Finnhub REST calls the app needs (https://finnhub.io/docs/api).
/// The free plan allows 60 calls a minute and has no price history (candles),
/// so <see cref="StockService"/> builds the charts from repeated quotes.
/// JSON is read with JsonDocument, which works in the Native AOT build.
/// </summary>
sealed class FinnhubClient {
    private readonly HttpClient http;

    public FinnhubClient(string apiKey) {
        var handler = new SocketsHttpHandler {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        this.http = new HttpClient(handler) {
            BaseAddress = new Uri("https://finnhub.io/api/v1/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        this.http.DefaultRequestHeaders.TryAddWithoutValidation("X-Finnhub-Token", apiKey);
        this.http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "EPDomino24-Stocks/1.0");
    }

    /// <summary>The latest quote, or null for a symbol Finnhub does not know (it answers with zeros).</summary>
    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken cancel) {
        using var doc = await this.GetAsync($"quote?symbol={Uri.EscapeDataString(symbol)}", cancel).ConfigureAwait(false);
        var r = doc.RootElement;
        var current = Num(r, "c") ?? 0;
        var time = (long)(Num(r, "t") ?? 0);
        if (current <= 0 && time == 0) return null;
        var prev = Num(r, "pc") ?? 0;
        var change = Num(r, "d") ?? current - prev;
        var percent = Num(r, "dp") ?? (prev > 0 ? change / prev * 100 : 0);
        return new Quote(current, change, percent, Num(r, "h") ?? 0, Num(r, "l") ?? 0, Num(r, "o") ?? 0, prev, time);
    }

    /// <summary>Name, exchange and industry. Funds (ETFs) have no profile, so their name comes from symbol search.</summary>
    public async Task<Profile?> GetProfileAsync(string symbol, CancellationToken cancel) {
        using (var doc = await this.GetAsync($"stock/profile2?symbol={Uri.EscapeDataString(symbol)}", cancel).ConfigureAwait(false)) {
            var r = doc.RootElement;
            var name = Str(r, "name");
            if (!string.IsNullOrEmpty(name)) {
                return new Profile(name, ShortExchange(Str(r, "exchange")), Str(r, "finnhubIndustry") ?? "",
                                   Str(r, "currency") ?? "USD", Num(r, "shareOutstanding") ?? 0);
            }
        }

        using var search = await this.GetAsync($"search?q={Uri.EscapeDataString(symbol)}&exchange=US", cancel).ConfigureAwait(false);
        if (search.RootElement.TryGetProperty("result", out var results) && results.ValueKind == JsonValueKind.Array) {
            foreach (var item in results.EnumerateArray()) {
                if (!string.Equals(Str(item, "symbol"), symbol, StringComparison.OrdinalIgnoreCase)) continue;
                var description = Str(item, "description") ?? symbol;
                if (description.EndsWith("-US", StringComparison.Ordinal)) description = description[..^3];
                if (description.StartsWith("SS SPDR", StringComparison.Ordinal)) description = description[3..];
                var type = Str(item, "type");
                return new Profile(description, "", type == "ETP" ? "Fund (ETF)" : type ?? "", "USD", 0);
            }
        }
        return null;
    }

    /// <summary>Adds the 52-week range, P/E, dividend yield and beta from /stock/metric.</summary>
    public async Task<Profile> AddMetricsAsync(string symbol, Profile profile, CancellationToken cancel) {
        using var doc = await this.GetAsync($"stock/metric?symbol={Uri.EscapeDataString(symbol)}&metric=all", cancel).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("metric", out var m) || m.ValueKind != JsonValueKind.Object) return profile;
        return profile with {
            WeekHigh52 = Num(m, "52WeekHigh"),
            WeekLow52 = Num(m, "52WeekLow"),
            PeRatio = Num(m, "peTTM") ?? Num(m, "peBasicExclExtraTTM"),
            DividendYield = Num(m, "dividendYieldIndicatedAnnual") ?? Num(m, "currentDividendYieldTTM"),
            Beta = Num(m, "beta"),
        };
    }

    public async Task<MarketStatus> GetMarketStatusAsync(CancellationToken cancel) {
        using var doc = await this.GetAsync("stock/market-status?exchange=US", cancel).ConfigureAwait(false);
        var r = doc.RootElement;
        var open = r.TryGetProperty("isOpen", out var o) && o.ValueKind == JsonValueKind.True;
        return new MarketStatus(open, Str(r, "session"), Str(r, "holiday"), (long)(Num(r, "t") ?? 0));
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancel) {
        using var response = await this.http.GetAsync(path, cancel).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var message = response.StatusCode switch {
                HttpStatusCode.TooManyRequests => "Finnhub rate limit reached",
                HttpStatusCode.Unauthorized => "Finnhub rejected the API key (check stocks.txt)",
                HttpStatusCode.Forbidden => "This data needs a paid Finnhub plan",
                _ => $"Finnhub answered {(int)response.StatusCode} {response.ReasonPhrase}",
            };
            throw new FinnhubException(response.StatusCode, message);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancel).ConfigureAwait(false);
    }

    private static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>"NASDAQ NMS - GLOBAL MARKET" → "NASDAQ".</summary>
    private static string ShortExchange(string? exchange) {
        if (string.IsNullOrEmpty(exchange)) return "";
        if (exchange.StartsWith("NASDAQ", StringComparison.OrdinalIgnoreCase)) return "NASDAQ";
        if (exchange.StartsWith("NEW YORK STOCK EXCHANGE", StringComparison.OrdinalIgnoreCase)) return "NYSE";
        return exchange;
    }

    /// <summary>A short, human message for a failed call.</summary>
    public static string Describe(Exception ex) => ex switch {
        FinnhubException f => f.Message,
        TaskCanceledException => "Finnhub took too long to answer",
        HttpRequestException { InnerException: System.Net.Sockets.SocketException se } =>
            se.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound ? "No internet (finnhub.io not found)" : "No connection to Finnhub",
        HttpRequestException { InnerException: System.Security.Authentication.AuthenticationException } =>
            "Secure connection failed (check the board's clock)",
        HttpRequestException => "No connection to Finnhub",
        JsonException => "Finnhub sent an unreadable answer",
        _ => ex.Message,
    };
}
