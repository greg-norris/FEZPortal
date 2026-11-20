using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using HolidayLights.App;
using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.Preview {
    /// <summary>
    /// Renders the Holiday Lights pages to PNGs on the PC with the same drawing code and fonts as the
    /// Portal, drives them with simulated touches, and checks the bytes that go to the strip.
    /// </summary>
    static class Program {
        class FakeStore : ISettingsStore {
            public string Saved;
            public bool Present => true;
            public bool Save(string text) {
                this.Saved = text;
                Console.WriteLine("(would save lights.txt)");
                return true;
            }
        }

        static string outDir;
        static int failures;

        static int Main(string[] args) {
            if (args.Length != 3) {
                Console.Error.WriteLine("usage: LightsPreview <font dir> <sd card dir> <out dir>");
                return 1;
            }
            var fontDir = args[0];
            var sdDir = args[1];
            outDir = args[2];
            Directory.CreateDirectory(outDir);

            // The sample file on the SD card copy must match the built-in defaults.
            var sample = File.ReadAllText(Path.Combine(sdDir, LightSettings.FileName)).Replace("\r\n", "\n");
            Check("SdCard lights.txt matches the built-in defaults", sample == LightSettings.DefaultText.Replace("\r\n", "\n"));

            var settings = new LightSettings();
            settings.Load(null);
            Check("defaults: 160 LEDs, GRB, 1 A, rainbow", settings.Count == 160 && LightSettings.Orders[settings.Order] == "GRB" &&
                  settings.PowerLimit == 1000 && settings.Effect == LightSettings.Rainbow);

            EncodingChecks();
            TouchChecks();

            var engine = new LightEngine(settings);
            var store = new FakeStore();
            var canvas = new Canvas(Theme.ScreenWidth, Theme.ScreenHeight, fontDir);
            var app = new LightsApp(settings, engine, store, canvas);
            var t = new DateTime(2026, 12, 24, 23, 0, 0);

            // Every main-page effect, a couple of seconds in.
            for (var effect = 0; effect < LightSettings.MainEffects; effect++) {
                var e = effect;
                var b = MainButton(e);
                app.Touch(0, b.X, b.Y);
                app.Touch(2, b.X, b.Y);
                if (effect == LightSettings.Chase) Tap(app, 200, 176);              // a blue-green from the colour bar
                if (effect == LightSettings.Twinkle) Tap(app, 442, 176);            // cool white
                if (effect == LightSettings.Comet) Tap(app, 40, 176);               // orange-red
                Run(app, engine, ref t, 2000);
                Save(canvas, "main-" + LightSettings.EffectNames[e].ToLower().Replace(" ", ""));
                Console.WriteLine(LightSettings.EffectNames[e] + ": about " + engine.Milliamps + " mA (wanted " + engine.WantedMilliamps + ")");
            }
            Check("tapping an effect selects it", settings.Effect == LightSettings.Classic);
            Check("Candy cane is off the main page", IndexOfName(LightSettings.EffectNames, "Candy cane") >= LightSettings.MainEffects);

            HolidayChecks(app, engine, canvas, settings, ref t);

            // Back on the main page, with Classic again for the checks below.
            var classic = MainButton(LightSettings.Classic);
            Tap(app, classic.X, classic.Y);
            Check("Classic is on again", settings.Effect == LightSettings.Classic && app.Current == app.Main);

            // Picking a colour while Classic runs switches to Solid.
            Tap(app, 120, 176);
            Check("a colour tap on Classic switches to Solid", settings.Effect == LightSettings.Solid && settings.Color >= 0);

            // Drag the brightness all the way up on warm white: the power limit kicks in.
            Tap(app, 376, 176);
            app.Touch(0, 200, 218);
            app.Touch(1, 300, 218);
            app.Touch(1, 470, 218);
            app.Touch(2, 470, 218);
            Check("dragging brightness to the end gives 100", settings.Brightness == 100);
            Run(app, engine, ref t, 100);
            Save(canvas, "main-limited");
            Check("warm white at 100% is held to the 1 A limit", engine.WantedMilliamps > 1000 && engine.Milliamps <= 1000);
            Console.WriteLine("warm white at 100%: wanted " + engine.WantedMilliamps + " mA, sent " + engine.Milliamps + " mA");

            // Speed slider and the hue drag.
            app.Touch(0, 118, 255);
            app.Touch(2, 118, 255);
            Check("speed at the left end is 0 (0.25x)", settings.Speed == 0 && Math.Abs(LightEngine.Rate(0) - 0.25) < 0.001);
            app.Touch(0, 20, 176);
            app.Touch(1, 338, 176);
            app.Touch(2, 338, 176);
            Check("dragging along the colour bar ends at the far end", settings.Color == 359);

            // Off.
            Tap(app, 440, 15);
            Run(app, engine, ref t, 100);
            Save(canvas, "main-off");
            Check("OFF sends all LEDs dark", !settings.On && engine.Milliamps == 0);
            Tap(app, 440, 15);

            // Saving waits for the finger to settle.
            Run(app, engine, ref t, 4000);
            Check("settings were saved", store.Saved != null && store.Saved.IndexOf("effect=solid") >= 0 && store.Saved.IndexOf("brightness=100") >= 0);
            var reloaded = new LightSettings();
            reloaded.Load(store.Saved);
            Check("saved file reads back the same", reloaded.Color == settings.Color && reloaded.Speed == settings.Speed &&
                  reloaded.Brightness == settings.Brightness && reloaded.Effect == settings.Effect);

            // Setup: the test pattern runs while it is open.
            Tap(app, 360, 15);
            Run(app, engine, ref t, 100);
            Save(canvas, "setup");
            Check("Setup turns the test pattern on", engine.TestPattern);
            Check("160 LEDs send 490 bytes, last 10 white", engine.SendLength == 490 && IsWhite(engine.Buffer, 5, 150) && IsWhite(engine.Buffer, 5, 159) &&
                  !IsWhite(engine.Buffer, 5, 149));
            Tap(app, 100, 126);     // RGB
            Tap(app, 410, 214);     // power limit +
            Run(app, engine, ref t, 100);
            Save(canvas, "setup-rgb");
            Check("order RGB and limit 1.25 A", LightSettings.Orders[settings.Order] == "RGB" && settings.PowerLimit == 1250);

            // A second 160-LED strip joined on: +32 five times.
            for (var i = 0; i < 5; i++) Tap(app, 450, 178);
            Run(app, engine, ref t, 300);
            Save(canvas, "setup-320");
            Check("count 320 after five +32 taps", settings.Count == 320 && engine.Count == 320);
            Check("320 LEDs send 980 bytes (10 latch bytes each end)", engine.SendLength == 980 && StripOutput.LatchBytes(320) == 10);
            Check("test pattern: last 10 of 320 white, the end of strip 1 off", IsWhite(engine.Buffer, 10, 310) && IsWhite(engine.Buffer, 10, 319) &&
                  !IsWhite(engine.Buffer, 10, 159) && engine.Buffer[10 + 159 * 3] == 0x80);
            Check("latch bytes after LED 320 are zero", engine.Buffer[10 + 320 * 3] == 0 && engine.Buffer[979] == 0);

            // Back to the main page: Rainbow along both strips, drawn as two rows.
            Tap(app, 30, 15);       // back
            Check("back leaves the test pattern", !engine.TestPattern && app.Current == app.Main);
            var rainbow = MainButton(LightSettings.Rainbow);
            Tap(app, rainbow.X, rainbow.Y);
            app.Touch(0, 200, 218);  // brightness back to 40%
            app.Touch(1, 230, 218);
            app.Touch(2, 230, 218);
            Run(app, engine, ref t, 2000);
            Save(canvas, "main-320-rainbow");
            Console.WriteLine("Rainbow on 320 LEDs at " + settings.Brightness + "%: about " + engine.Milliamps + " mA (wanted " + engine.WantedMilliamps + ")");
            Check("rainbow lights the second strip too", engine.Buffer[10 + 300 * 3] != 0x80 || engine.Buffer[10 + 300 * 3 + 1] != 0x80 || engine.Buffer[10 + 300 * 3 + 2] != 0x80);

            // Back down to one strip: LEDs 161-320 are still sent, as off.
            Tap(app, 360, 15);
            for (var i = 0; i < 5; i++) Tap(app, 246, 178);   // -32
            Tap(app, 30, 15);
            Run(app, engine, ref t, 300);
            Check("back to 160: still sends 320 LEDs, the second strip off", settings.Count == 160 && engine.SendLength == 980 &&
                  engine.Buffer[10 + 200 * 3] == 0x80 && engine.Buffer[10 + 319 * 3 + 2] == 0x80 && engine.Buffer[10 + 100 * 3] != 0x80);

            Console.WriteLine(failures == 0 ? "All checks passed." : failures + " check(s) FAILED.");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>The Holidays button, the Holidays page and every holiday's themes.</summary>
        static void HolidayChecks(LightsApp app, LightEngine engine, Canvas canvas, LightSettings settings, ref DateTime t) {
            var holidays = MainButton(LightSettings.MainEffects);   // the last button
            Tap(app, holidays.X, holidays.Y);
            Run(app, engine, ref t, 100);
            Save(canvas, "holidays");
            Check("the last main button opens the Holidays page", app.Current == app.HolidayMenu && settings.Effect == LightSettings.Classic);

            for (var h = 0; h < Holidays.Names.Length; h++) {
                var list = Holidays.EffectsOf(h);
                Check(Holidays.Names[h] + " has 1 to 4 themes", list.Length >= 1 && list.Length <= 4);
                var hb = HolidayButton(h);
                Tap(app, hb.X, hb.Y);
                Check("tapping " + Holidays.Names[h] + " opens its themes", app.Current == app.ThemeMenu && app.ThemeMenu.Holiday == h);
                for (var i = 0; i < list.Length; i++) {
                    var tb = ThemeButton(i);
                    Tap(app, tb.X, tb.Y);
                    Check("tapping " + LightSettings.EffectNames[list[i]] + " starts it", settings.Effect == list[i] && settings.On);
                    // Long enough for fireworks and lightning to show up.
                    Run(app, engine, ref t, 3000);
                    var lit = 0;
                    for (var k = 0; k < engine.Count; k++) {
                        var p = 5 + k * 3;
                        if (engine.Buffer[p] != 0x80 || engine.Buffer[p + 1] != 0x80 || engine.Buffer[p + 2] != 0x80) lit++;
                    }
                    var theme = LightSettings.ThemeOf(list[i]);
                    Save(canvas, "theme-" + theme.Key);
                    Console.WriteLine(Holidays.Names[h] + " / " + theme.Name + ": " + lit + " LEDs lit, about " + engine.Milliamps + " mA (wanted " + engine.WantedMilliamps + ")");
                    Check(theme.Name + " (" + Holidays.Names[h] + ") lights the strip", lit > 0 && engine.Milliamps <= settings.PowerLimit);
                }
                Tap(app, 30, 15);   // back to the Holidays page
                Check("back from " + Holidays.Names[h] + " goes to the Holidays page", app.Current == app.HolidayMenu);
            }

            // The Holidays page and button show the holiday that is on.
            var xmas = Holidays.EffectsOf(Holidays.Christmas);
            var christmas = HolidayButton(Holidays.Christmas);
            Tap(app, christmas.X, christmas.Y);
            Save(canvas, "christmas-before");
            var first = ThemeButton(0);
            Tap(app, first.X, first.Y);
            Check("Candy cane is the first Christmas theme", settings.Effect == xmas[0] && LightSettings.EffectNames[xmas[0]] == "Candy cane");
            app.Touch(0, 200, 254);   // speed slider works here too
            app.Touch(1, 259, 254);
            app.Touch(2, 259, 254);
            Check("speed slider on a holiday page", settings.Speed == 50);
            Run(app, engine, ref t, 2000);
            Save(canvas, "christmas");

            // Candy cane is still red and white stripes, five LEDs wide.
            var frame = new int[160];
            var fx = new Effects(160);
            fx.Render(xmas[0], 0, 160, 0, 0, frame);
            Check("Candy cane: 5 red, 5 white", frame[0] == 0xFF0000 && frame[4] == 0xFF0000 && frame[5] == 0xFFFFFF && frame[9] == 0xFFFFFF && frame[10] == 0xFF0000);

            Tap(app, 30, 15);
            Run(app, engine, ref t, 100);
            Save(canvas, "holidays-christmas-on");
            Tap(app, 30, 15);
            Run(app, engine, ref t, 100);
            Save(canvas, "main-holiday-on");
            Check("back twice reaches the main page", app.Current == app.Main);

            // lights.txt: a theme saves and reads back; the old "candycane" name still works.
            var copy = new LightSettings();
            copy.Load(settings.ToText());
            Check("a holiday theme saves and reads back", copy.Effect == settings.Effect && settings.ToText().IndexOf("effect=christmas-candycane") >= 0);
            copy.Load("effect=candycane");
            Check("effect=candycane from an old lights.txt is Christmas candy cane", copy.Effect == xmas[0]);
            for (var i = 0; i < LightSettings.EffectNames.Length; i++) {
                copy.Effect = i;
                var back = new LightSettings();
                back.Load(copy.ToText());
                if (back.Effect != i) Check("effect " + LightSettings.EffectNames[i] + " reads back", false);
            }
        }

        static int IndexOfName(string[] names, string name) {
            for (var i = 0; i < names.Length; i++) {
                if (names[i] == name) return i;
            }
            return -1;
        }

        static Point HolidayButton(int h) => new Point(6 + h % 3 * 158 + 76, 54 + h / 3 * 72 + 33);
        static Point ThemeButton(int i) => new Point(6 + i % 2 * 237 + 115, 54 + i / 2 * 70 + 32);

        /// <summary>Taps the driver reported late still come through as one Down and one Up.</summary>
        static void TouchChecks() {
            var t = new TouchTracker();
            t.Pressed(10, 20);
            t.Moved(12, 21);
            t.Moved(14, 22);
            t.Lifted(14, 22, true);
            Check("normal tap: down, one merged move, up", Events(t) == "D10,20 M14,22 U14,22");

            t.Moved(100, 50);                    // the press itself was missed
            t.Lifted(101, 50, true);
            Check("missed press: the first move starts the touch", Events(t) == "D100,50 U101,50");

            t.Lifted(200, 60, true);             // only the lift was seen
            Check("quick tap seen only at the lift still taps", Events(t) == "D200,60 U200,60");

            t.Lifted(0, 0, false);               // a lift with no position and no touch: nothing
            Check("a lift with no touch and no position is ignored", Events(t) == "");

            t.Pressed(5, 5);
            t.Pressed(300, 100);                 // the lift in between was missed
            t.Lifted(0, 0, false);
            Check("missed lift: the old touch ends before the new one", Events(t) == "D5,5 U5,5 D300,100 U300,100");
        }

        /// <summary>Whether LED i (after the latch bytes) is sent as white (all three bytes the same and lit).</summary>
        static bool IsWhite(byte[] buffer, int latch, int i) {
            var p = latch + i * 3;
            return buffer[p] > 0x80 && buffer[p] == buffer[p + 1] && buffer[p] == buffer[p + 2];
        }

        static string Events(TouchTracker t) {
            var text = "";
            int[] e;
            while ((e = t.Next()) != null) text += (text.Length > 0 ? " " : "") + "DMU"[e[0]] + e[1] + "," + e[2];
            return text;
        }

        /// <summary>The bytes for a few known frames.</summary>
        static void EncodingChecks() {
            var frame = new int[160];
            var buffer = new byte[StripOutput.BufferSize(160)];
            int wanted;
            Check("160 LEDs: 5 latch bytes each end, 490 bytes", StripOutput.LatchBytes(160) == 5 && buffer.Length == 490);

            frame[0] = 0xFF0000;    // red
            frame[1] = 0x00FF00;    // green
            frame[2] = 0x0000FF;    // blue
            var ma = StripOutput.Encode(frame, 160, 100, 0, 10000, buffer, out wanted);   // GRB
            Check("GRB red is 80 FF 80", buffer[5] == 0x80 && buffer[6] == 0xFF && buffer[7] == 0x80);
            Check("GRB green is FF 80 80", buffer[8] == 0xFF && buffer[9] == 0x80 && buffer[10] == 0x80);
            Check("GRB blue is 80 80 FF", buffer[11] == 0x80 && buffer[12] == 0x80 && buffer[13] == 0xFF);
            Check("unlit LEDs are 0x80", buffer[14] == 0x80 && buffer[484] == 0x80);
            Check("latch bytes are zero", buffer[0] == 0 && buffer[4] == 0 && buffer[485] == 0 && buffer[489] == 0);
            Check("three channels at full: 60 mA", ma == 60 && wanted == 60);

            StripOutput.Encode(frame, 160, 100, 1, 10000, buffer, out wanted);   // RGB
            Check("RGB red is FF 80 80", buffer[5] == 0xFF && buffer[6] == 0x80 && buffer[7] == 0x80);

            for (var i = 0; i < 160; i++) frame[i] = 0xFFFFFF;
            ma = StripOutput.Encode(frame, 160, 100, 0, 1000, buffer, out wanted);
            Check("full white wants 9.6 A and is held to 1 A", wanted == 9600 && ma <= 1000 && ma > 900);
            Console.WriteLine("full white: wanted " + wanted + " mA, sent " + ma + " mA, LED byte " + (buffer[5] & 0x7F) + "/127");

            StripOutput.Encode(frame, 160, 0, 0, 1000, buffer, out wanted);
            Check("brightness 0 is dark", wanted == 0 && buffer[5] == 0x80);
        }

        /// <summary>Runs the light thread and the UI loop side by side for a while, 25 ms at a time.</summary>
        static void Run(LightsApp app, LightEngine engine, ref DateTime t, int ms) {
            for (var elapsed = 0; elapsed < ms; elapsed += 25) {
                t = t.AddMilliseconds(25);
                engine.Step(t);
                app.Update(t);
            }
        }

        static Point MainButton(int effect) => new Point(5 + effect % 4 * 119 + 56, 54 + effect / 4 * 50 + 22);

        static void Tap(LightsApp app, int x, int y) {
            app.Touch(0, x, y);
            app.Touch(2, x, y);
        }

        static void Check(string what, bool ok) {
            Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
            if (!ok) failures++;
        }

        static void Save(Canvas canvas, string name) {
            var path = Path.Combine(outDir, name + ".png");
            canvas.Bitmap.Save(path, ImageFormat.Png);
            using (var zoom = new Bitmap(960, 544))
            using (var g = Graphics.FromImage(zoom)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(canvas.Bitmap, 0, 0, 960, 544);
                zoom.Save(Path.Combine(outDir, name + ".2x.png"), ImageFormat.Png);
            }
            Console.WriteLine("Wrote " + path);
        }
    }
}
