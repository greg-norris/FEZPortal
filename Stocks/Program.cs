// Stocks - EPDomino24 (EPM815 / STM32MP153) + 7" 800x480 parallel-RGB panel
// with the FT5xx6 capacitive touch controller.
//
// A stock watchlist with live quotes from Finnhub (https://finnhub.io):
//   - the home screen shows up to 8 stocks per page (swipe for more): price,
//     today's change and a small chart of the day;
//   - tap a stock for its detail screen: a big intraday chart, open/high/low,
//     day and 52-week ranges, market cap, P/E and dividend yield; swipe to step
//     through the watchlist, tap "‹ Watchlist" to go back.
// The API key and the watchlist are in stocks.txt, the Wi-Fi network in wifi.txt.

using System.Runtime.InteropServices;

using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.UI;

using Stocks;

const int Width = 800;
const int Height = 480;

// -- Display (same panel timings as HelloWorld and Browser) -------------------

// A display program that is still shutting down turns the panel off while this
// one turns it on, and the kernel's display driver can crash on that overlap
// (seen as an Oops in drm_connector_list_iter_next). Let it finish first.
WaitForOtherDisplayApps(TimeSpan.FromSeconds(8));

DisplayController.SetSettings(new DisplaySettings {
    Type = DisplayType.Parallel,
    EnableAtBoot = false,
    Backlight = new BacklightSettings {
        Pin = GHIElectronics.Endpoint.Core.EPDomino24.Backlight,
        ActivePolarity = SignalPolarity.ActiveHigh,
    },
    Parallel = new ParallelLcdSettings {
        Width = Width,
        Height = Height,
        PixelClockHz = 33_000_000,
        HFrontPorch = 40, HSync = 48, HBackPorch = 88,
        VFrontPorch = 13, VSync = 3, VBackPorch = 32,
        BitsPerPixel = 16,
        DataEnablePolarity = SignalPolarity.ActiveHigh,
        // The 7" 800x480 panel samples pixel data on the falling edge.
        PixelClockEdge = ClockEdge.Falling,
    },
});

using var display = DisplayController.GetDefault()
    ?? throw new InvalidOperationException("GetDefault returned null; display.json type is None?");
display.Enable();

UI.Initialize();

// -- Touch -------------------------------------------------------------------

var touchInput = new TouchInput();
Ft5xx6Touch? touch = null;
try {
    touch = new Ft5xx6Touch();
    touch.OnChangedEvent += (_, e) => touchInput.Post(e);
}
catch (Exception ex) {
    Console.Error.WriteLine($"Touch controller not available: {ex.Message}");
}

// -- Data and screens ----------------------------------------------------------

var folder = AppContext.BaseDirectory;
var settings = Settings.Load(Path.Combine(folder, "stocks.txt"));
var wifi = new WifiManager(Path.Combine(folder, "wifi.txt"));
wifi.Start();
var service = new StockService(settings, folder);
service.Start();

var assets = new Assets();
var app = new StocksApp(assets, service, wifi, touchInput, settings.Problem);
var scripted = DebugInput.FromEnvironment(touchInput);

using var tick = new UiTimer(20, () => {
    try {
        var now = Environment.TickCount64;
        scripted?.Tick(now);
        touchInput.Pump();
        app.Tick(now);
    }
    catch (Exception ex) {
        Console.Error.WriteLine($"UI tick failed: {ex}");
    }
});

using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, c => { c.Cancel = true; UI.Stop(); });
using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, c => { c.Cancel = true; UI.Stop(); });

Console.WriteLine($"Stocks running ({settings.Symbols.Count} symbols). Ctrl-C to quit.");
if (settings.Problem is not null) Console.Error.WriteLine(settings.Problem);
UI.Run();

touch?.Dispose();
Console.WriteLine("Stocks done.");

// ---------------------------------------------------------------------------

static void WaitForOtherDisplayApps(TimeSpan timeout) {
    if (!Directory.Exists("/proc")) return;
    var self = Environment.ProcessId;
    var end = DateTime.UtcNow + timeout;
    List<string> others;
    do {
        others = Directory.GetDirectories("/proc").Select(Path.GetFileName)
            .Where(d => int.TryParse(d, out var pid) && pid != self && IsDisplayApp(pid)).ToList()!;
        if (others.Count == 0) return;
        Console.WriteLine($"Waiting for the display program (pid {string.Join(", ", others)}) to exit...");
        Thread.Sleep(250);
    } while (DateTime.UtcNow < end);
    Console.Error.WriteLine("Another display program is still running (stop it with \"endpoint-cli stop app\"); starting anyway.");

    static bool IsDisplayApp(int pid) {
        try {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            if (stat.Contains(") Z ")) return false;   // already exited (zombie)
            var args = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0', StringSplitOptions.RemoveEmptyEntries);
            return args.Any(a => a.EndsWith("/Stocks") || a.EndsWith("/Stocks.dll") || a is "Stocks" or "Stocks.dll"
                              || a.EndsWith("/Browser") || a.EndsWith("/Browser.dll") || a is "Browser" or "Browser.dll");
        }
        catch {
            return false;
        }
    }
}
