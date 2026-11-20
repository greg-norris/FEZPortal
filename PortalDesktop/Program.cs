using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Display;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Pins;
using PortalDesktop.App;
using PortalDesktop.Data;
using PortalDesktop.Device;
using PortalDesktop.Ui;

namespace PortalDesktop {
    class Program {
        /// <summary>
        /// Shows the screen upside down (rotated 180°), for a Portal mounted the other way up.
        /// Touch is flipped to match. Set to false for the normal orientation.
        /// </summary>
        const bool UpsideDown = true;

        static void Main() {
            var canvas = new Canvas(Graphics.FromHdc(StartDisplay()));
            var state = new PortalState();

            // Show something right away; the SD card and Wi-Fi take a moment.
            canvas.FillRect(Theme.Background, 0, 0, Theme.ScreenWidth, Theme.ScreenHeight);
            canvas.DrawTextCentered("Starting FEZ Portal Desktop...", UiFont.Large, Theme.Panel, Theme.ScreenWidth / 2, 124);
            canvas.Flush();

            TimeSync.LoadFromRtc();

            var storage = new Storage();
            storage.Load(state);

            var data = new DataService(state, storage);
            var shell = new Shell(state, data, canvas);

            TouchInput touch = null;
            try {
                touch = new TouchInput(UpsideDown);
            }
            catch (Exception ex) {
                Debug.WriteLine("Touch: " + ex.Message);
            }

            data.Start();

            while (true) {
                if (touch != null) {
                    int[] e;
                    while ((e = touch.Next()) != null) {
                        if (e[0] == TouchInput.Tap) shell.Tap(e[1], e[2]);
                        else if (e[0] == TouchInput.SwipeUp || e[0] == TouchInput.SwipeDown) shell.VerticalSwipe(e[0] == TouchInput.SwipeUp ? 1 : -1);
                        else shell.Swipe(e[0] == TouchInput.SwipeNext ? 1 : -1);
                    }
                }
                shell.Update();
                Thread.Sleep(30);
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
