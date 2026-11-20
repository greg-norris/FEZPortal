using System.Globalization;
using System.Reflection;

using GHIElectronics.Endpoint.UI;

using UiFont = GHIElectronics.Endpoint.UI.Font;

namespace Stocks;

/// <summary>A rectangle in screen pixels.</summary>
readonly record struct Rect(int X, int Y, int W, int H) {
    public bool Contains(int x, int y) => x >= this.X && x < this.X + this.W && y >= this.Y && y < this.Y + this.H;
}

/// <summary>The embedded font (Noto Sans) at the few sizes the app uses, and its text metrics.</summary>
sealed class Assets {
    // Each size is a separate native font, so the set is kept small.
    public const int S14 = 14, S16 = 16, S20 = 20, S26 = 26, S32 = 32, S48 = 48;

    private readonly byte[] fontBytes;
    private readonly Dictionary<int, UiFont> fonts = new();

    public Assets() {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Stocks.Resources.NotoSans-Regular.ttf")
            ?? throw new FileNotFoundException("Embedded font missing");
        this.fontBytes = new byte[stream.Length];
        stream.ReadExactly(this.fontBytes);
        this.Metrics = new TextMetrics(this.fontBytes);
    }

    public TextMetrics Metrics { get; }

    public UiFont Font(int sizePx) {
        if (!this.fonts.TryGetValue(sizePx, out var font)) {
            font = UiFont.Load(this.fontBytes, sizePx);
            this.fonts[sizePx] = font;
        }
        return font;
    }

    /// <summary>A label box tall enough for one line at <paramref name="sizePx"/>.</summary>
    public int LineHeight(int sizePx) => this.Metrics.LineHeight(sizePx) + 2;
}

/// <summary>Small factories for the widgets the screens are built from.</summary>
static class Ui {
    /// <summary>A plain filled rectangle (no border line, no padding) that can hold children.</summary>
    public static Border Box(Widget parent, int x, int y, int w, int h, uint color, int radius = 0) => new(parent) {
        Thickness = 0,
        Padding = 0,
        CornerRadius = radius,
        BackgroundColor = color,
        X = x, Y = y, Width = w, Height = h,
    };

    public static Label Text(Widget parent, Assets assets, int size, int x, int y, int w, uint color,
                             TextAlignment align = TextAlignment.Left, string text = "") => new(parent) {
        Font = assets.Font(size),
        Color = color,
        TextAlignment = align,
        Text = text,
        X = x, Y = y, Width = w, Height = assets.LineHeight(size),
    };

    /// <summary>Sets a label's text only when it changed (each change redraws it).</summary>
    public static void Set(Label label, string text, uint? color = null) {
        if (label.Text != text) label.Text = text;
        if (color is { } c && label.Color != c) label.Color = c;
    }
}

/// <summary>Number formats used on screen (always invariant: "1,234.56").</summary>
static class Fmt {
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public static string Price(double v, string currency = "USD") => Symbol(currency) + Plain(v);

    public static string Plain(double v) =>
        Math.Abs(v) >= 1000 ? v.ToString("#,0.00", C) : Math.Abs(v) >= 1 ? v.ToString("0.00", C) : v.ToString("0.0000", C);

    public static string Change(double v) => (v >= 0 ? "+" : "-") + Plain(Math.Abs(v));

    public static string Percent(double v) => (v >= 0 ? "+" : "-") + Math.Abs(v).ToString("0.00", C) + "%";

    /// <summary>Market value from Finnhub's millions: "$3.41T", "$812.5B", "$950M".</summary>
    public static string Money(double millions, string currency = "USD") {
        var s = Symbol(currency);
        if (millions >= 1_000_000) return s + (millions / 1_000_000).ToString("0.00", C) + "T";
        if (millions >= 1_000) return s + (millions / 1_000).ToString("0.0", C) + "B";
        return s + millions.ToString("0", C) + "M";
    }

    public static string Time(DateTime et) => et.ToString("h:mm tt", C);

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{Math.Max(1, (int)Math.Ceiling(t.TotalMinutes))}m";

    private static string Symbol(string currency) => currency switch {
        "USD" or "" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        _ => currency + " ",
    };
}
