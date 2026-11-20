using GHIElectronics.Endpoint.UI;
using GHIElectronics.Endpoint.UI.Drawing;

using UiImage = GHIElectronics.Endpoint.UI.Image;

namespace Stocks;

/// <summary>Where prices and times land on a drawn chart (for placing axis labels).</summary>
sealed record ChartScale(double Low, double High, int Top, int Bottom, long Start, long End, int PlotWidth) {
    public int Y(double price) => this.Top + (int)Math.Round((this.High - price) / (this.High - this.Low) * (this.Bottom - this.Top));

    public int X(long time) => (int)Math.Round(Math.Clamp((double)(time - this.Start) / (this.End - this.Start), 0, 1) * (this.PlotWidth - 1));
}

/// <summary>
/// An intraday price chart drawn on a canvas. The x axis is the whole regular
/// session (9:30 to 16:00 New York time), so the line grows across the day.
/// </summary>
sealed class ChartView : IDisposable {
    private readonly Border host;
    private readonly int width, height;
    private Canvas? canvas;
    private UiImage? image;

    /// <param name="parent">Labels added to <paramref name="parent"/> after this view stay on top of the chart.</param>
    public ChartView(Widget parent, int x, int y, int width, int height, uint background) {
        this.width = width;
        this.height = height;
        this.host = Ui.Box(parent, x, y, width, height, background);
        this.Background = background;
    }

    public uint Background { get; }

    /// <summary>Space kept free on the right of the plot (for price labels).</summary>
    public int RightGutter { get; init; }

    /// <summary>Draw the price grid, hour lines and a fatter line (the detail screen).</summary>
    public bool Detailed { get; init; }

    public ChartScale? Scale { get; private set; }

    /// <summary>Price levels with grid lines (detail charts only).</summary>
    public List<double> Ticks { get; } = new();

    public void Render(StockView view) {
        var c = Canvas.Create(this.width, this.height, PixelFormat.Rgb565);
        try {
            this.Draw(c, view);
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Chart {view.Symbol}: {ex.Message}");
        }

        // The new image goes up before the old one goes, so nothing flickers.
        var newImage = new UiImage(this.host) { X = 0, Y = 0, Width = this.width, Height = this.height };
        newImage.SetSource(c.Buffer, this.width, this.height);
        this.image?.Dispose();          // releases its hold on the old canvas buffer
        this.canvas?.Dispose();
        this.image = newImage;
        this.canvas = c;
    }

    private void Draw(Canvas c, StockView view) {
        c.Clear(Color.FromRgb(this.Background));
        this.Scale = null;
        this.Ticks.Clear();

        var q = view.Quote;
        var lastTime = q?.Time ?? (view.Samples.Length > 0 ? view.Samples[^1].Time : 0);
        if (lastTime <= 0) return;
        var date = MarketClock.SessionDate(lastTime);
        var (open, close) = MarketClock.Session(date);
        var samples = view.Samples.Where(s => MarketClock.SessionDate(s.Time) == date).ToArray();

        // Price range: the samples, the day's high and low, and yesterday's close.
        var values = samples.Select(s => s.Price).ToList();
        if (q is not null) {
            values.Add(q.Current);
            if (q.PrevClose > 0) values.Add(q.PrevClose);
            if (q.High > 0) values.Add(q.High);
            if (q.Low > 0) values.Add(q.Low);
        }
        if (values.Count == 0) return;
        double low = values.Min(), high = values.Max();
        var minSpan = Math.Max(high * 0.004, 0.01);
        if (high - low < minSpan) {
            var mid = (high + low) / 2;
            low = mid - minSpan / 2;
            high = mid + minSpan / 2;
        }
        var pad = (high - low) * 0.08;
        low -= pad;
        high += pad;

        var plotW = this.width - this.RightGutter;
        var top = this.Detailed ? 10 : 4;
        var bottom = this.height - (this.Detailed ? 10 : 4);
        var scale = new ChartScale(low, high, top, bottom, open, close, plotW);
        this.Scale = scale;

        var up = q?.IsUp ?? (samples.Length < 2 || samples[^1].Price >= samples[0].Price);
        var lineColor = Theme.UpDown(up);
        var fill = Color.FromRgb(Theme.Blend(this.Background, lineColor, 0.18));
        var line = Color.FromRgb(lineColor);

        if (this.Detailed) {
            var grid = Color.FromRgb(Theme.Grid);
            var step = NiceStep((high - low) / 4);
            for (var v = Math.Ceiling(low / step) * step; v <= high; v += step) {
                this.Ticks.Add(v);
                c.DrawLine(0, scale.Y(v), this.width - 1, scale.Y(v), grid, 1);
            }
            for (var hour = 10; hour <= 15; hour++) {
                var x = scale.X(MarketClock.Unix(date, TimeSpan.FromHours(hour)));
                c.DrawLine(x, top, x, bottom, grid, 1);
            }
        }

        // One point per pixel column (the last price in it) keeps drawing cheap.
        var points = new List<(int X, int Y)>();
        foreach (var s in samples) {
            var p = (scale.X(s.Time), scale.Y(s.Price));
            if (points.Count > 0 && points[^1].X == p.Item1) points[^1] = p;
            else points.Add(p);
        }

        // Shading under the line.
        for (var i = 0; i < points.Count; i++) {
            var (x0, y0) = points[i];
            var (x1, y1) = i + 1 < points.Count ? points[i + 1] : points[i];
            for (var x = x0; x <= x1; x++) {
                var y = x1 == x0 ? y0 : y0 + (y1 - y0) * (x - x0) / (x1 - x0);
                if (y < bottom) c.FillRectangle(x, y, 1, bottom - y + 1, fill);
            }
        }

        // Yesterday's close, dashed.
        if (q is { PrevClose: > 0 }) {
            var y = scale.Y(q.PrevClose);
            var dash = Color.FromRgb(Theme.Faint);
            for (var x = 0; x < plotW; x += 8) c.DrawLine(x, y, Math.Min(x + 3, plotW - 1), y, dash, 1);
        }

        var thickness = this.Detailed ? 3 : 2;
        for (var i = 1; i < points.Count; i++) {
            c.DrawLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, line, thickness);
        }
        if (points.Count > 0) {
            var (lx, ly) = points[^1];
            c.FillCircle(lx, ly, this.Detailed ? 5 : 3, line);
        }
    }

    /// <summary>1, 2, 2.5 or 5 times a power of ten, at least <paramref name="rough"/>.</summary>
    private static double NiceStep(double rough) {
        if (rough <= 0) return 1;
        var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 }) {
            if (m * power >= rough) return m * power;
        }
        return 10 * power;
    }

    public void Dispose() {
        this.image?.Dispose();
        this.canvas?.Dispose();
        this.image = null;
        this.canvas = null;
        this.host.Dispose();
    }
}
