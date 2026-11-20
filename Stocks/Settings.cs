namespace Stocks;

/// <summary>
/// stocks.txt next to the program:
///   apikey=YOUR_FINNHUB_KEY
///   symbols=AAPL,MSFT,NVDA
/// </summary>
sealed class Settings {
    public const int MaxSymbols = 24;

    public string ApiKey { get; private init; } = "";
    public IReadOnlyList<string> Symbols { get; private init; } = Array.Empty<string>();

    /// <summary>Why the settings cannot be used, or null when they are fine.</summary>
    public string? Problem { get; private init; }

    public static Settings Load(string path) {
        if (!File.Exists(path)) return new Settings { Problem = $"{Path.GetFileName(path)} is missing" };

        var key = "";
        var symbols = new List<string>();
        foreach (var raw in File.ReadAllLines(path)) {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var name = line[..eq].Trim().ToLowerInvariant();
            var value = line[(eq + 1)..].Trim();
            if (name is "apikey" or "key" or "token") key = value;
            if (name is "symbols" or "watchlist") {
                foreach (var s in value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                    var symbol = s.ToUpperInvariant();
                    if (!symbols.Contains(symbol) && symbols.Count < MaxSymbols) symbols.Add(symbol);
                }
            }
        }

        string? problem = null;
        if (key.Length == 0) problem = "No Finnhub API key in stocks.txt (apikey=...)";
        else if (symbols.Count == 0) problem = "No symbols in stocks.txt (symbols=AAPL,MSFT,...)";
        return new Settings { ApiKey = key, Symbols = symbols, Problem = problem };
    }
}
