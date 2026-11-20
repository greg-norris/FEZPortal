using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using GHIElectronics.TinyCLR.Drivers.FocalTech.FT5xx6;
using GHIElectronics.TinyCLR.Pins;
using HolidayLights.Ui;

namespace HolidayLights.Device {
    /// <summary>
    /// The Portal's capacitive touch panel (FT5xx6 on I2C1, interrupt on PG9). Flips the points to
    /// match the screen and hands them to a <see cref="TouchTracker"/>, which copes with presses the
    /// driver reports late.
    /// </summary>
    public class TouchInput {
        readonly FT5xx6Controller touch;
        readonly TouchTracker tracker = new TouchTracker();
        readonly bool flip;

        /// <param name="flip">True when the display is rotated 180°: touch points are turned to match.</param>
        public TouchInput(bool flip) {
            this.flip = flip;
            var i2c = I2cController.FromName(SC20260.I2cBus.I2c1);
            var device = i2c.GetDevice(new I2cConnectionSettings(0x38) {
                BusSpeed = 100000,
                AddressFormat = I2cAddressFormat.SevenBit,
            });
            var interrupt = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PG9);
            this.touch = new FT5xx6Controller(device, interrupt);
            this.touch.TouchDown += (s, e) => this.tracker.Pressed(this.X(e.X), this.Y(e.Y));
            this.touch.TouchMove += (s, e) => this.tracker.Moved(this.X(e.X), this.Y(e.Y));
            // The panel keeps the last point on a lift; (0, 0) means it has none.
            this.touch.TouchUp += (s, e) => this.tracker.Lifted(this.X(e.X), this.Y(e.Y), e.X != 0 || e.Y != 0);
        }

        int X(int x) => this.flip ? Theme.ScreenWidth - 1 - x : x;
        int Y(int y) => this.flip ? Theme.ScreenHeight - 1 - y : y;

        /// <summary>The next event as { kind, x, y } (kind: TouchTracker.Down, Move or Up), or null.</summary>
        public int[] Next() => this.tracker.Next();
    }
}
