using System.Globalization;

using GHIElectronics.Endpoint.UI;

namespace Stocks;

/// <summary>A full-screen page of the app.</summary>
interface IAppScreen : ITouchScreen {
    void Show();

    /// <summary>Redraw what changed (<paramref name="force"/>: everything).</summary>
    void Refresh(bool force);

    /// <summary>Clock, market state and status line only.</summary>
    void UpdateHeader();
}

/// <summary>
/// The bar along the top of both screens: title (or back), market state,
/// optional pager, New York time and the data status.
/// </summary>
sealed class Header {
    public const int Height = 52;
    public static readonly Rect TitleZone = new(0, 0, 210, Height);
    public static readonly Rect PrevZone = new(410, 0, 90, Height);
    public static readonly Rect NextZone = new(500, 0, 90, Height);

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly Assets assets;
    private readonly StockService service;
    private readonly WifiManager wifi;
    private readonly Label title, market, pager, clock, status;
    private readonly Border dot;
    private readonly uint titleColor;
    private const int MarketW = 250;

    public Header(Widget parent, Assets assets, StockService service, WifiManager wifi, string titleText, bool back) {
        this.assets = assets;
        this.service = service;
        this.wifi = wifi;
        Ui.Box(parent, 0, 0, 800, Height, Theme.Header);
        Ui.Box(parent, 0, Height, 800, 1, Theme.Divider);
        this.titleColor = back ? Theme.Accent : Theme.Text;
        this.title = back
            ? Ui.Text(parent, assets, Assets.S20, 14, 13, 190, this.titleColor, text: titleText)
            : Ui.Text(parent, assets, Assets.S26, 16, 7, 190, this.titleColor, text: titleText);
        this.dot = Ui.Box(parent, 158, 22, 10, 10, Theme.Faint, 5);
        this.market = Ui.Text(parent, assets, Assets.S16, 174, 14, MarketW, Theme.SubText);
        this.pager = Ui.Text(parent, assets, Assets.S20, 430, 12, 140, Theme.Text, TextAlignment.Center);
        this.clock = Ui.Text(parent, assets, Assets.S16, 580, 4, 204, Theme.Text, TextAlignment.Right);
        this.status = Ui.Text(parent, assets, Assets.S14, 420, 28, 364, Theme.Faint, TextAlignment.Right);
    }

    public void SetPager(int current, int count) =>
        Ui.Set(this.pager, count > 1 ? $"‹   {current + 1} / {count}   ›" : "");

    public void SetTitlePressed(bool pressed) => this.title.Color = pressed ? Theme.Text : this.titleColor;

    public void Update() {
        Ui.Set(this.clock, MarketClock.EasternNow.ToString("ddd MMM d  h:mm tt", Inv) + " ET");

        var (text, color) = MarketText(this.service.Market);
        Ui.Set(this.market, this.assets.Metrics.Ellipsize(text, Assets.S16, MarketW), Theme.SubText);
        if (this.dot.BackgroundColor != color) this.dot.BackgroundColor = color;

        string line;
        uint lineColor;
        if (this.service.StatusIsError) {
            line = !this.wifi.Connected && this.service.Status.StartsWith("No ", StringComparison.Ordinal) ? this.wifi.Status : this.service.Status;
            lineColor = Theme.Warning;
        }
        else if (this.service.LastUpdateUnix > 0) {
            line = "Updated " + MarketClock.ToEastern(this.service.LastUpdateUnix).ToString("h:mm:ss tt", Inv);
            lineColor = Theme.Faint;
        }
        else {
            line = this.wifi.Connected || this.service.Status.Length > 0 && this.wifi.Status.StartsWith("No Wi-Fi", StringComparison.Ordinal)
                ? this.service.Status : this.wifi.Status;
            lineColor = Theme.SubText;
        }
        Ui.Set(this.status, this.assets.Metrics.Ellipsize(line, Assets.S14, 364), lineColor);
    }

    private static (string Text, uint Dot) MarketText(MarketStatus? m) {
        if (m is null) return ("Checking the market…", Theme.Faint);
        var now = MarketClock.EasternNow;
        var today = now.Date;
        if (m.IsOpen) {
            var left = today + MarketClock.SessionClose - now;
            return (left > TimeSpan.Zero ? $"Market open · closes in {Fmt.Duration(left)}" : "Market open", Theme.Up);
        }
        if (!string.IsNullOrEmpty(m.Holiday)) return ($"Closed · {m.Holiday}", Theme.Faint);
        switch (m.Session) {
            case "pre-market": {
                var until = today + MarketClock.SessionOpen - now;
                return (until > TimeSpan.Zero ? $"Pre-market · opens in {Fmt.Duration(until)}" : "Pre-market", Theme.Warning);
            }
            case "post-market":
                return ("After hours", Theme.Warning);
            default:
                return ("Market closed", Theme.Faint);
        }
    }
}

/// <summary>One stock on the watchlist: symbol, name, price, change and a small chart.</summary>
sealed class Card {
    public const int W = 186, H = 200;
    private const int PillW = 66, SymbolW = W - 12 - PillW - 12;   // "GOOGL" fits at 26 px

    private readonly Assets assets;
    private readonly Border box, pill;
    private readonly Label symbol, name, price, change, percent;
    private readonly ChartView chart;
    private int shownVersion = -1;
    private string? shownSymbol;

    public Card(Widget parent, Assets assets, int x, int y) {
        this.assets = assets;
        this.Bounds = new Rect(x, y, W, H);
        this.box = new Border(parent) {
            Thickness = 2,
            BorderBrush = Theme.Card,
            Padding = 0,
            CornerRadius = 12,
            BackgroundColor = Theme.Card,
            X = x, Y = y, Width = W, Height = H,
        };
        this.symbol = Ui.Text(this.box, assets, Assets.S26, 12, 6, SymbolW, Theme.Text);
        this.pill = Ui.Box(this.box, W - 12 - PillW, 12, PillW, 26, Theme.Card, 13);
        this.percent = Ui.Text(this.pill, assets, Assets.S14, 0, 2, PillW, Theme.Text, TextAlignment.Center);
        this.name = Ui.Text(this.box, assets, Assets.S14, 12, 44, W - 24, Theme.SubText);
        this.price = Ui.Text(this.box, assets, Assets.S32, 12, 62, W - 24, Theme.Text);
        this.change = Ui.Text(this.box, assets, Assets.S16, 12, 108, W - 24, Theme.SubText);
        this.chart = new ChartView(this.box, 8, 136, W - 20, 54, Theme.Card);
    }

    public Rect Bounds { get; }

    public string? Symbol => this.shownSymbol;

    public void SetPressed(bool pressed) => this.box.BorderBrush = pressed ? Theme.CardPressed : Theme.Card;

    public void Hide() {
        this.box.Visible = false;
        this.shownSymbol = null;
    }

    public void Bind(StockView v, bool force) {
        this.box.Visible = true;
        if (!force && v.Symbol == this.shownSymbol && v.Version == this.shownVersion) return;
        this.shownSymbol = v.Symbol;
        this.shownVersion = v.Version;

        var m = this.assets.Metrics;
        Ui.Set(this.symbol, m.Ellipsize(v.Symbol, Assets.S26, SymbolW));
        Ui.Set(this.name, m.Ellipsize(v.Name.Length > 0 ? v.Name : " ", Assets.S14, W - 24));

        if (v.Quote is not { } q) {
            Ui.Set(this.price, "—", Theme.Faint);
            Ui.Set(this.change, v.Error ?? "Loading…", v.Error is null ? Theme.Faint : Theme.Warning);
            Ui.Set(this.percent, "");
            this.pill.BackgroundColor = Theme.Card;
        }
        else {
            var color = Theme.UpDown(q.IsUp);
            var text = Fmt.Price(q.Current, v.Profile?.Currency ?? "USD");
            var size = m.Measure(text, Assets.S32) <= W - 24 ? Assets.S32 : Assets.S26;
            this.price.Font = this.assets.Font(size);
            Ui.Set(this.price, text, Theme.Text);
            Ui.Set(this.change, $"{Fmt.Change(q.Change)} today", color);
            Ui.Set(this.percent, Fmt.Percent(q.ChangePercent), color);
            var pillColor = Theme.Blend(Theme.Card, color, 0.22);
            if (this.pill.BackgroundColor != pillColor) this.pill.BackgroundColor = pillColor;
        }
        this.chart.Render(v);
    }
}

/// <summary>The home screen: the watchlist as pages of 4 × 2 cards.</summary>
sealed class WatchlistScreen : IAppScreen {
    public const int PerPage = 8;
    private const int Cols = 4, Gap = 10, Left = 13, Top = Header.Height + 8;

    private readonly Screen screen;
    private readonly Header header;
    private readonly StockService service;
    private readonly Card[] cards = new Card[PerPage];
    private readonly Label message;
    private readonly Action<int> open;
    private readonly bool unusable;
    private int page;
    private Card? pressed;

    public WatchlistScreen(Assets assets, StockService service, WifiManager wifi, string? problem, Action<int> open) {
        this.service = service;
        this.open = open;
        this.unusable = problem is not null;
        this.screen = new Screen { BackgroundColor = Theme.Background };
        this.header = new Header(this.screen, assets, service, wifi, "Stocks", back: false);
        for (var i = 0; i < PerPage; i++) {
            var x = Left + (i % Cols) * (Card.W + Gap);
            var y = Top + (i / Cols) * (Card.H + Gap);
            this.cards[i] = new Card(this.screen, assets, x, y);
        }
        this.message = Ui.Text(this.screen, assets, Assets.S20, 40, 220, 720, Theme.Warning, TextAlignment.Center, problem ?? "");
        this.message.Visible = problem is not null;
    }

    private int Count => this.unusable ? 0 : this.service.Symbols.Count;
    private int Pages => Math.Max(1, (this.Count + PerPage - 1) / PerPage);

    public void ShowSymbol(int index) {
        this.page = Math.Clamp(index / PerPage, 0, this.Pages - 1);
        this.Show();
    }

    public void Show() {
        this.screen.Load();
        this.Refresh(true);
    }

    public void Refresh(bool force) {
        this.UpdateHeader();
        for (var i = 0; i < PerPage; i++) {
            var index = this.page * PerPage + i;
            if (index < this.Count) this.cards[i].Bind(this.service.Get(this.service.Symbols[index]), force);
            else this.cards[i].Hide();
        }
    }

    public void UpdateHeader() {
        this.header.SetPager(this.page, this.Pages);
        this.header.Update();
    }

    private void GoToPage(int page) {
        page = Math.Clamp(page, 0, this.Pages - 1);
        if (page == this.page) return;
        this.page = page;
        this.Refresh(true);
    }

    public void Press(int x, int y) {
        this.pressed = this.cards.FirstOrDefault(c => c.Symbol is not null && c.Bounds.Contains(x, y));
        this.pressed?.SetPressed(true);
    }

    public void CancelPress() {
        this.pressed?.SetPressed(false);
        this.pressed = null;
    }

    public void Tap(int x, int y) {
        if (this.Pages > 1 && Header.PrevZone.Contains(x, y)) {
            this.GoToPage(this.page - 1);
            return;
        }
        if (this.Pages > 1 && Header.NextZone.Contains(x, y)) {
            this.GoToPage(this.page + 1);
            return;
        }
        for (var i = 0; i < PerPage; i++) {
            if (this.cards[i].Symbol is not null && this.cards[i].Bounds.Contains(x, y)) {
                this.open(this.page * PerPage + i);
                return;
            }
        }
    }

    public void Swipe(int direction) => this.GoToPage(this.page + direction);
}

/// <summary>A label pair on the stats panel: name on the left, value on the right.</summary>
sealed class StatRow {
    private readonly Label value;

    public StatRow(Widget parent, Assets assets, int y, string name) {
        Ui.Text(parent, assets, Assets.S14, 14, y + 1, 90, Theme.SubText, text: name);
        this.value = Ui.Text(parent, assets, Assets.S16, 14, y - 1, 156, Theme.Text, TextAlignment.Right);
    }

    public void Set(string text, uint color = Theme.Text) => Ui.Set(this.value, text, color);
}

/// <summary>Low–high bar with a marker at the current price.</summary>
sealed class RangeBar {
    private const int TrackW = 156, MarkerW = 6;

    private readonly Border marker;
    private readonly Label low, high;

    public RangeBar(Widget parent, Assets assets, int y, string title) {
        Ui.Text(parent, assets, Assets.S14, 14, y, 156, Theme.SubText, text: title);
        Ui.Box(parent, 14, y + 28, TrackW, 6, Theme.Track, 3);
        this.marker = Ui.Box(parent, 14, y + 23, MarkerW, 16, Theme.Text, 3);
        this.low = Ui.Text(parent, assets, Assets.S14, 14, y + 40, 78, Theme.Faint);
        this.high = Ui.Text(parent, assets, Assets.S14, 92, y + 40, 78, Theme.Faint, TextAlignment.Right);
    }

    public void Set(double? low, double? high, double? current, uint color) {
        if (low is not { } lo || high is not { } hi || current is not { } c || hi <= lo || lo <= 0) {
            Ui.Set(this.low, "—");
            Ui.Set(this.high, "");
            this.marker.Visible = false;
            return;
        }
        Ui.Set(this.low, Fmt.Plain(lo));
        Ui.Set(this.high, Fmt.Plain(hi));
        this.marker.Visible = true;
        this.marker.X = 14 + (int)Math.Round(Math.Clamp((c - lo) / (hi - lo), 0, 1) * (TrackW - MarkerW));
        if (this.marker.BackgroundColor != color) this.marker.BackgroundColor = color;
    }
}

/// <summary>One stock in detail: big price, the day's chart and key numbers.</summary>
sealed class DetailScreen : IAppScreen {
    private const int ChartX = 16, ChartY = 192, ChartW = 572, ChartH = 244, Gutter = 64;
    private const int PanelX = 600, PanelW = 188;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly (string Text, int Hour, int Minute)[] TimeMarks =
        { ("9:30", 9, 30), ("11 AM", 11, 0), ("1 PM", 13, 0), ("3 PM", 15, 0), ("4 PM", 16, 0) };

    private readonly Assets assets;
    private readonly StockService service;
    private readonly Action back;
    private readonly Screen screen;
    private readonly Header header;
    private readonly Label symbol, name, price, change, asOf;
    private readonly ChartView chart;
    private readonly List<Label> priceLabels = new();
    private readonly List<Label> timeLabels = new();
    private readonly StatRow open, high, low, prevClose, marketCap, pe, dividend;
    private readonly RangeBar dayRange, yearRange;
    private readonly Label industry;
    private int index;
    private int shownVersion = -1;
    private bool backPressed;

    public DetailScreen(Assets assets, StockService service, WifiManager wifi, Action back) {
        this.assets = assets;
        this.service = service;
        this.back = back;
        this.screen = new Screen { BackgroundColor = Theme.Background };
        this.header = new Header(this.screen, assets, service, wifi, "‹  Watchlist", back: true);

        this.symbol = Ui.Text(this.screen, assets, Assets.S32, 20, 56, 200, Theme.Text);
        this.name = Ui.Text(this.screen, assets, Assets.S16, 120, 68, 460, Theme.SubText);
        this.price = Ui.Text(this.screen, assets, Assets.S48, 20, 90, 330, Theme.Text);
        this.change = Ui.Text(this.screen, assets, Assets.S26, 240, 112, 340, Theme.SubText);
        this.asOf = Ui.Text(this.screen, assets, Assets.S14, 20, 162, 570, Theme.Faint);

        // The chart first, so the axis labels created after it sit on top.
        this.chart = new ChartView(this.screen, ChartX, ChartY, ChartW, ChartH, Theme.Background) { RightGutter = Gutter, Detailed = true };
        for (var i = 0; i < 8; i++) {
            this.priceLabels.Add(Ui.Text(this.screen, assets, Assets.S14, ChartX + ChartW - Gutter + 6, 0, Gutter - 6, Theme.Faint));
        }
        foreach (var mark in TimeMarks) {
            this.timeLabels.Add(Ui.Text(this.screen, assets, Assets.S14, 0, ChartY + ChartH + 2, 60, Theme.Faint, TextAlignment.Center, mark.Text));
        }

        var panel = Ui.Box(this.screen, PanelX, Header.Height + 8, PanelW, 480 - Header.Height - 16, Theme.Card, 12);
        this.open = new StatRow(panel, assets, 12, "Open");
        this.high = new StatRow(panel, assets, 38, "High");
        this.low = new StatRow(panel, assets, 64, "Low");
        this.prevClose = new StatRow(panel, assets, 90, "Prev close");
        this.dayRange = new RangeBar(panel, assets, 122, "Day range");
        this.yearRange = new RangeBar(panel, assets, 190, "52-week range");
        this.marketCap = new StatRow(panel, assets, 264, "Market cap");
        this.pe = new StatRow(panel, assets, 290, "P/E");
        this.dividend = new StatRow(panel, assets, 316, "Div. yield");
        Ui.Text(panel, assets, Assets.S14, 14, 348, 156, Theme.SubText, text: "Industry");
        this.industry = Ui.Text(panel, assets, Assets.S16, 14, 370, 156, Theme.Text);
    }

    public int Index => this.index;

    public void Open(int index) {
        this.index = index;
        this.shownVersion = -1;
        this.screen.Load();
        this.Refresh(true);
    }

    public void Show() => this.Open(this.index);

    public void UpdateHeader() {
        this.header.SetPager(this.index, this.service.Symbols.Count);
        this.header.Update();
    }

    public void Refresh(bool force) {
        this.UpdateHeader();
        var v = this.service.Get(this.service.Symbols[this.index]);
        if (!force && v.Version == this.shownVersion) return;
        this.shownVersion = v.Version;

        var m = this.assets.Metrics;
        var p = v.Profile;
        var q = v.Quote;
        var currency = p?.Currency ?? "USD";

        Ui.Set(this.symbol, v.Symbol);
        var nameX = 20 + m.Measure(v.Symbol, Assets.S32) + 14;
        this.name.X = nameX;
        var nameText = p is null ? "" : p.Exchange.Length > 0 ? $"{p.Name} · {p.Exchange}" : p.Name;
        Ui.Set(this.name, m.Ellipsize(nameText, Assets.S16, ChartX + ChartW - nameX));

        if (q is null) {
            Ui.Set(this.price, "—", Theme.Faint);
            Ui.Set(this.change, "");
            Ui.Set(this.asOf, v.Error ?? "Loading…", v.Error is null ? Theme.Faint : Theme.Warning);
        }
        else {
            var color = Theme.UpDown(q.IsUp);
            var priceText = Fmt.Price(q.Current, currency);
            Ui.Set(this.price, priceText, Theme.Text);
            this.change.X = 20 + m.Measure(priceText, Assets.S48) + 18;
            Ui.Set(this.change, $"{Fmt.Change(q.Change)}  ({Fmt.Percent(q.ChangePercent)})", color);

            var et = MarketClock.ToEastern(q.Time);
            var note = $"Last trade {et.ToString("h:mm tt", Inv)} ET, {et.ToString("ddd MMM d", Inv)}";
            var date = MarketClock.SessionDate(q.Time);
            var points = v.Samples.Count(s => MarketClock.SessionDate(s.Time) == date);
            if (points <= 2) note += "  ·  the chart fills in while this app runs";
            if (v.Error is not null) note = v.Error;
            Ui.Set(this.asOf, note, v.Error is null ? Theme.Faint : Theme.Warning);
        }

        this.chart.Render(v);
        this.PlaceAxisLabels();
        this.FillPanel(v);
    }

    private void PlaceAxisLabels() {
        var scale = this.chart.Scale;
        var lineH = this.assets.LineHeight(Assets.S14);
        for (var i = 0; i < this.priceLabels.Count; i++) {
            var label = this.priceLabels[i];
            if (scale is null || i >= this.chart.Ticks.Count) {
                label.Visible = false;
                continue;
            }
            var y = scale.Y(this.chart.Ticks[i]);
            var visible = y - lineH / 2 >= 0 && y + lineH / 2 <= ChartH;
            label.Visible = visible;
            if (!visible) continue;
            Ui.Set(label, Fmt.Plain(this.chart.Ticks[i]));
            label.Y = ChartY + y - lineH / 2;
        }

        for (var i = 0; i < TimeMarks.Length; i++) {
            var label = this.timeLabels[i];
            label.Visible = scale is not null;
            if (scale is null) continue;
            var date = MarketClock.SessionDate(scale.Start);
            var x = scale.X(MarketClock.Unix(date, new TimeSpan(TimeMarks[i].Hour, TimeMarks[i].Minute, 0)));
            label.X = ChartX + Math.Clamp(x - 30, -12, ChartW - 60);
        }
    }

    private void FillPanel(StockView v) {
        var q = v.Quote;
        var p = v.Profile;
        string Price(double? x) => x is > 0 ? Fmt.Plain(x.Value) : "—";

        this.open.Set(Price(q?.Open));
        this.high.Set(Price(q?.High));
        this.low.Set(Price(q?.Low));
        this.prevClose.Set(Price(q?.PrevClose));
        var color = q is null ? Theme.Text : Theme.UpDown(q.IsUp);
        this.dayRange.Set(q?.Low, q?.High, q?.Current, color);
        this.yearRange.Set(p?.WeekLow52, p?.WeekHigh52, q?.Current, color);

        this.marketCap.Set(p is { SharesMillions: > 0 } && q is not null ? Fmt.Money(p.SharesMillions * q.Current, p.Currency) : "—");
        this.pe.Set(p?.PeRatio is { } pe && pe > 0 ? pe.ToString("0.0", Inv) : "—");
        this.dividend.Set(p?.DividendYield is { } y && y > 0 ? y.ToString("0.00", Inv) + "%" : "—");
        Ui.Set(this.industry, this.assets.Metrics.Ellipsize(p is { Industry.Length: > 0 } ? p.Industry : "—", Assets.S16, 156));
    }

    private void Step(int direction) {
        var n = this.service.Symbols.Count;
        if (n <= 1) return;
        this.Open((this.index + direction + n) % n);
    }

    public void Press(int x, int y) {
        this.backPressed = Header.TitleZone.Contains(x, y);
        if (this.backPressed) this.header.SetTitlePressed(true);
    }

    public void CancelPress() {
        if (this.backPressed) this.header.SetTitlePressed(false);
        this.backPressed = false;
    }

    public void Tap(int x, int y) {
        if (Header.TitleZone.Contains(x, y)) this.back();
        else if (Header.PrevZone.Contains(x, y)) this.Step(-1);
        else if (Header.NextZone.Contains(x, y)) this.Step(1);
    }

    public void Swipe(int direction) => this.Step(direction);
}

/// <summary>Switches between the watchlist and the detail screen and keeps the visible one current.</summary>
sealed class StocksApp {
    private readonly StockService service;
    private readonly TouchInput touch;
    private readonly WatchlistScreen list;
    private readonly DetailScreen detail;
    private IAppScreen current;
    private int shownVersion = -1;
    private long nextHeader;

    public StocksApp(Assets assets, StockService service, WifiManager wifi, TouchInput touch, string? problem) {
        this.service = service;
        this.touch = touch;
        this.list = new WatchlistScreen(assets, service, wifi, problem, this.OpenDetail);
        this.detail = new DetailScreen(assets, service, wifi, this.BackToList);
        this.current = this.list;
        this.list.Show();
        touch.Screen = this.list;
    }

    private void OpenDetail(int index) {
        this.detail.Open(index);
        this.Switch(this.detail);
    }

    private void BackToList() {
        this.list.ShowSymbol(this.detail.Index);
        this.Switch(this.list);
    }

    private void Switch(IAppScreen screen) {
        this.current = screen;
        this.touch.Screen = screen;
    }

    public void Tick(long now) {
        var version = this.service.Version;
        if (version != this.shownVersion) {
            this.shownVersion = version;
            this.current.Refresh(false);
            this.nextHeader = now + 1000;
        }
        else if (now >= this.nextHeader) {
            this.current.UpdateHeader();
            this.nextHeader = now + 1000;
        }
    }
}
