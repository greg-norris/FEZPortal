using System.Collections.Concurrent;

using GHIElectronics.Endpoint.Devices.Touch;

namespace Stocks;

/// <summary>What a screen does with the finger.</summary>
interface ITouchScreen {
    /// <summary>A finger went down at (x, y); show a pressed look.</summary>
    void Press(int x, int y);

    /// <summary>The press turned into a swipe or left the screen; drop the pressed look.</summary>
    void CancelPress();

    void Tap(int x, int y);

    /// <summary>A horizontal swipe: +1 = finger moved left (next), -1 = moved right (previous).</summary>
    void Swipe(int direction);
}

/// <summary>
/// Turns raw touch events into taps and horizontal swipes. Events arrive on
/// the touch driver's thread and are handled in <see cref="Pump"/> on the UI
/// thread. The app draws its own screens, so LVGL gets no touch input.
/// </summary>
sealed class TouchInput {
    private const int TapSlop = 14;
    private const int SwipeMin = 70;

    private enum Kind { Down, Move, Up }

    private readonly ConcurrentQueue<(Kind Kind, int X, int Y)> queue = new();
    private bool down, moved;
    private int downX, downY, lastX, lastY;

    public ITouchScreen? Screen { get; set; }

    /// <summary>From the touch driver (any thread).</summary>
    public void Post(TouchEventArgs e) {
        if (e.EventType == TouchEventType.Released || e.Points.Length == 0) this.queue.Enqueue((Kind.Up, 0, 0));
        else this.queue.Enqueue((e.EventType == TouchEventType.Pressed ? Kind.Down : Kind.Move, e.Points[0].X, e.Points[0].Y));
    }

    public void Down(int x, int y) => this.queue.Enqueue((Kind.Down, x, y));
    public void Move(int x, int y) => this.queue.Enqueue((Kind.Move, x, y));
    public void Up() => this.queue.Enqueue((Kind.Up, 0, 0));

    public void Pump() {
        while (this.queue.TryDequeue(out var e)) {
            var screen = this.Screen;
            switch (e.Kind) {
                case Kind.Down:
                    this.down = true;
                    this.moved = false;
                    this.downX = this.lastX = e.X;
                    this.downY = this.lastY = e.Y;
                    screen?.Press(e.X, e.Y);
                    break;
                case Kind.Move when this.down:
                    this.lastX = e.X;
                    this.lastY = e.Y;
                    if (!this.moved && (Math.Abs(e.X - this.downX) > TapSlop || Math.Abs(e.Y - this.downY) > TapSlop)) {
                        this.moved = true;
                        screen?.CancelPress();
                    }
                    break;
                case Kind.Up when this.down:
                    this.down = false;
                    var dx = this.lastX - this.downX;
                    var dy = this.lastY - this.downY;
                    if (!this.moved) {
                        screen?.CancelPress();
                        screen?.Tap(this.downX, this.downY);
                    }
                    else if (Math.Abs(dx) >= SwipeMin && Math.Abs(dx) > Math.Abs(dy)) {
                        screen?.Swipe(dx < 0 ? 1 : -1);
                    }
                    break;
            }
        }
    }
}

/// <summary>
/// Scripted touches for testing without a finger on the panel. Only active
/// when the STOCKS_INPUT environment variable names a file; the program polls
/// that file, plays its commands and deletes it:
///   tap X Y  |  drag X1 Y1 X2 Y2  |  wait MS
/// </summary>
sealed class DebugInput {
    private readonly string path;
    private readonly TouchInput input;
    private readonly List<(long Due, Action Action)> schedule = new();
    private long nextCheck;

    private DebugInput(string path, TouchInput input) {
        this.path = path;
        this.input = input;
    }

    public static DebugInput? FromEnvironment(TouchInput input) {
        var path = Environment.GetEnvironmentVariable("STOCKS_INPUT");
        if (string.IsNullOrEmpty(path)) return null;
        Console.WriteLine($"Scripted touch input from {path}");
        return new DebugInput(path, input);
    }

    public void Tick(long now) {
        while (this.schedule.Count > 0 && this.schedule[0].Due <= now) {
            var action = this.schedule[0].Action;
            this.schedule.RemoveAt(0);
            action();
        }
        if (this.schedule.Count > 0 || now < this.nextCheck) return;
        this.nextCheck = now + 100;
        if (!File.Exists(this.path)) return;

        string[] lines;
        try {
            lines = File.ReadAllLines(this.path);
            File.Delete(this.path);
        }
        catch (IOException) {
            return;
        }

        var t = now;
        foreach (var line in lines) {
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f.Length == 0) continue;
            try {
                switch (f[0]) {
                    case "tap": {
                        int x = int.Parse(f[1]), y = int.Parse(f[2]);
                        this.schedule.Add((t, () => this.input.Down(x, y)));
                        this.schedule.Add((t + 100, this.input.Up));
                        t += 400;
                        break;
                    }
                    case "drag": {
                        int x1 = int.Parse(f[1]), y1 = int.Parse(f[2]), x2 = int.Parse(f[3]), y2 = int.Parse(f[4]);
                        this.schedule.Add((t, () => this.input.Down(x1, y1)));
                        for (var k = 1; k <= 10; k++) {
                            int x = x1 + (x2 - x1) * k / 10, y = y1 + (y2 - y1) * k / 10;
                            this.schedule.Add((t + 30 * k, () => this.input.Move(x, y)));
                        }
                        this.schedule.Add((t + 360, this.input.Up));
                        t += 700;
                        break;
                    }
                    case "wait":
                        t += int.Parse(f[1]);
                        break;
                    default:
                        Console.Error.WriteLine($"STOCKS_INPUT: unknown command '{line}'");
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException) {
                Console.Error.WriteLine($"STOCKS_INPUT: bad line '{line}'");
            }
        }
    }
}
