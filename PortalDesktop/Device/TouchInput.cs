using System.Collections;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using GHIElectronics.TinyCLR.Drivers.FocalTech.FT5xx6;
using GHIElectronics.TinyCLR.Pins;
using PortalDesktop.Ui;

namespace PortalDesktop.Device {
    /// <summary>
    /// The Portal's capacitive touch panel (FT5xx6 on I2C1, interrupt on PG9). Turns finger
    /// down/up into taps and left/right/up/down swipes, queued for the UI thread.
    /// </summary>
    public class TouchInput {
        public const int Tap = 0, SwipeNext = 1, SwipePrevious = 2, SwipeUp = 3, SwipeDown = 4;
        const int SwipeDistance = 60;

        readonly FT5xx6Controller touch;
        readonly ArrayList queue = new ArrayList();
        readonly bool flip;
        int downX, downY, lastX, lastY;
        bool down;

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
            this.touch.TouchDown += (s, e) => {
                this.down = true;
                this.downX = this.lastX = this.X(e.X);
                this.downY = this.lastY = this.Y(e.Y);
            };
            this.touch.TouchMove += (s, e) => {
                this.lastX = this.X(e.X);
                this.lastY = this.Y(e.Y);
            };
            this.touch.TouchUp += (s, e) => {
                if (!this.down) return;
                this.down = false;
                var dx = this.lastX - this.downX;
                var dy = this.lastY - this.downY;
                var adx = dx < 0 ? -dx : dx;
                var ady = dy < 0 ? -dy : dy;
                int kind;
                if (dx <= -SwipeDistance && adx > 2 * ady) kind = SwipeNext;
                else if (dx >= SwipeDistance && adx > 2 * ady) kind = SwipePrevious;
                else if (dy <= -SwipeDistance && ady > 2 * adx) kind = SwipeUp;
                else if (dy >= SwipeDistance && ady > 2 * adx) kind = SwipeDown;
                else kind = Tap;
                lock (this.queue) this.queue.Add(new[] { kind, this.downX, this.downY });
            };
        }

        // Panel coordinates to screen coordinates. Swipes are worked out after flipping, so
        // left/right and up/down still match what the user sees.
        int X(int x) => this.flip ? Theme.ScreenWidth - 1 - x : x;
        int Y(int y) => this.flip ? Theme.ScreenHeight - 1 - y : y;

        /// <summary>The next event as { kind, x, y }, or null.</summary>
        public int[] Next() {
            lock (this.queue) {
                if (this.queue.Count == 0) return null;
                var e = (int[])this.queue[0];
                this.queue.RemoveAt(0);
                return e;
            }
        }
    }
}
