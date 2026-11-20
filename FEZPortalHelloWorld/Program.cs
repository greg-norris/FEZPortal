using System.Drawing;
using System.Threading;
using FEZPortalHelloWorld.Ui;
using GHIElectronics.TinyCLR.Devices.Display;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Pins;

namespace FEZPortalHelloWorld {
    class Program {
        const int ScreenWidth = 480;
        const int ScreenHeight = 272;

        static void Main() {
            // Turn on the display backlight.
            var backlight = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PA15);
            backlight.SetDriveMode(GpioPinDriveMode.Output);
            backlight.Write(GpioPinValue.High);

            // Configure the FEZ Portal's 4.3" 480x272 display.
            var displayController = DisplayController.GetDefault();
            displayController.SetConfiguration(new ParallelDisplayControllerSettings {
                Width = ScreenWidth,
                Height = ScreenHeight,
                DataFormat = DisplayDataFormat.Rgb565,
                Orientation = DisplayOrientation.Degrees0,
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
            displayController.Enable();

            var canvas = new Canvas(Graphics.FromHdc(displayController.Hdc));
            var dashboard = new Dashboard();
            dashboard.Draw(canvas);
            canvas.Flush();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
