using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Display;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Pins;
using HolidayLights.App;
using HolidayLights.Device;
using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights {
    /// <summary>
    /// Holiday Lights: drives an LPD8806 RGB LED strip (GE32RGB8806A, 160 LEDs) on SPI2 from the
    /// FEZ Portal's touch screen. A light thread sends ~40 frames a second to the strip; the main
    /// thread runs the UI.
    /// </summary>
    class Program {
        /// <summary>
        /// Shows the screen upside down (rotated 180°), for a Portal mounted the other way up, like
        /// PortalDesktop. Touch is flipped to match. Set to false for the normal orientation.
        /// </summary>
        const bool UpsideDown = true;

        // The light thread aims for a frame every 30 ms, but after each frame it always rests at least
        // 12 ms, and at least as long as the frame took to work out. So it never uses more than half the
        // CPU, however long the strip: with more LEDs the animation gets fewer frames a second instead of
        // the touch screen getting slow. Still patterns (Solid, Off, the test pattern) are resent twice a
        // second in case the strip missed a frame.
        const int FrameMs = 30, MinRestMs = 12, ResendMs = 500;

        class SdStore : ISettingsStore {
            readonly Storage storage;
            public SdStore(Storage storage) { this.storage = storage; }
            public bool Present => this.storage.Present;
            public bool Save(string text) => this.storage.WriteText(LightSettings.FileName, text);
        }

        static void Main() {
            var canvas = new Canvas(Graphics.FromHdc(StartDisplay()));
            canvas.FillRect(Theme.Background, 0, 0, Theme.ScreenWidth, Theme.ScreenHeight);
            canvas.DrawTextCentered("Starting Holiday Lights...", UiFont.Large, Theme.TextMuted, Theme.ScreenWidth / 2, 124);
            canvas.Flush();

            // Settings from the SD card (written there the first time, so they can be edited on a PC).
            var storage = new Storage();
            storage.Mount();
            var settings = new LightSettings();
            var text = storage.ReadText(LightSettings.FileName);
            if (text == null && storage.Present) storage.WriteText(LightSettings.FileName, LightSettings.DefaultText);
            settings.Load(text);

            var engine = new LightEngine(settings);
            LedStrip strip = null;
            try {
                strip = new LedStrip();
            }
            catch (Exception ex) {
                Debug.WriteLine("Strip: " + ex.Message);
            }
            var lights = new Thread(() => RunLights(engine, strip));
            lights.Priority = ThreadPriority.BelowNormal;
            lights.Start();

            var app = new LightsApp(settings, engine, new SdStore(storage), canvas);

            TouchInput touch = null;
            try {
                touch = new TouchInput(UpsideDown);
            }
            catch (Exception ex) {
                Debug.WriteLine("Touch: " + ex.Message);
            }

            while (true) {
                if (touch != null) {
                    int[] e;
                    while ((e = touch.Next()) != null) app.Touch(e[0], e[1], e[2]);
                }
                app.Update(DateTime.UtcNow);
                Thread.Sleep(20);
            }
        }

        /// <summary>The light thread: work out a frame, send it, rest until the next one.</summary>
        static void RunLights(LightEngine engine, LedStrip strip) {
            var lastSend = DateTime.MinValue;
            var nextLog = DateTime.UtcNow.AddSeconds(10);
            long workTicks = 0;
            var frames = 0;
            while (true) {
                var start = DateTime.UtcNow;
                try {
                    var changed = engine.Step(start);
                    if (strip != null && (changed || start < lastSend || start >= lastSend.AddMilliseconds(ResendMs))) {
                        strip.Write(engine.Buffer, engine.SendLength);
                        lastSend = start;
                    }
                    if (changed) frames++;
                }
                catch (Exception ex) {
                    Debug.WriteLine("Lights: " + ex.Message);
                }
                var end = DateTime.UtcNow;
                var work = (end - start).Ticks;
                if (work > 0) workTicks += work;
                var ms = (int)(work / TimeSpan.TicksPerMillisecond);

                // How busy the light thread is, for the Output window when debugging.
                if (end >= nextLog) {
                    Debug.WriteLine("Lights: " + frames / 10 + " frames/s, " +
                                    (frames > 0 ? workTicks / frames / TimeSpan.TicksPerMillisecond : 0) + " ms each");
                    frames = 0;
                    workTicks = 0;
                    nextLog = end.AddSeconds(10);
                }
                var rest = FrameMs - ms;
                if (rest < MinRestMs) rest = MinRestMs;
                if (rest < ms) rest = ms;
                Thread.Sleep(rest);
            }
        }

        /// <summary>Turns on the backlight and sets up the Portal's 4.3" 480x272 display.</summary>
        static IntPtr StartDisplay() {
            var backlight = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PA15);
            backlight.SetDriveMode(GpioPinDriveMode.Output);
            backlight.Write(GpioPinValue.High);

            var display = DisplayController.GetDefault();
            display.SetConfiguration(new ParallelDisplayControllerSettings {
                Width = Theme.ScreenWidth,
                Height = Theme.ScreenHeight,
                DataFormat = DisplayDataFormat.Rgb565,
                Orientation = UpsideDown ? DisplayOrientation.Degrees180 : DisplayOrientation.Degrees0,
                PixelClockRate = 10000000,
                PixelPolarity = false,
                DataEnablePolarity = false,
                DataEnableIsFixed = false,
                HorizontalFrontPorch = 2,
                HorizontalBackPorch = 2,
                HorizontalSyncPulseWidth = 41,
                HorizontalSyncPolarity = false,
                VerticalFrontPorch = 2,
                VerticalBackPorch = 2,
                VerticalSyncPulseWidth = 10,
                VerticalSyncPolarity = false,
            });
            display.Enable();
            return display.Hdc;
        }
    }
}
