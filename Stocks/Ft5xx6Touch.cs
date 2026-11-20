// User-space driver for the FocalTech FT5xx6 capacitive touch controller
// used on the EPDomino24 7" 800x480 panel.
//
// Wiring on the board:
//   - I2C5              (EPM815.I2c.I2c5)
//   - 7-bit address     0x38
//   - Reset pin         PF2 (active-low pulse to come out of reset)
//   - IRQ pin           PB11 (chip pulls low when a touch frame is ready)
//
// The library's Touch base class expects customer code to drive I2C chips
// like FT5xx6 and raise events via RaiseChanged.

using System.Device.Gpio;
using System.Device.I2c;

using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Touch;

namespace Stocks;

public sealed class Ft5xx6Touch : Touch {
    private const byte Address = 0x38;
    private const int Bus = EPM815.I2c.I2c5;

    private readonly GpioController gpio;
    private readonly int irqPin = EPM815.Gpio.Pin.PB11;
    private readonly int resetPin = EPM815.Gpio.Pin.PF2;
    private readonly I2cDevice i2c;
    private readonly byte[] readBuffer = new byte[7];
    private int lastPointCount;
    private bool disposed;

    public override int Width => 800;
    public override int Height => 480;
    public override bool Connected => !this.disposed;

    public Ft5xx6Touch() {
        this.gpio = new GpioController(new EPM815.Gpio.Driver());

        // Hard-reset pulse: low for 100 ms, then high; FT5xx6 needs ~300 ms
        // after rising edge before it answers I2C reliably.
        this.gpio.OpenPin(this.resetPin, PinMode.Output);
        this.gpio.Write(this.resetPin, PinValue.Low);
        Thread.Sleep(100);
        this.gpio.Write(this.resetPin, PinValue.High);
        Thread.Sleep(300);

        I2cController.Initialize(Bus);
        this.i2c = I2cDevice.Create(new I2cConnectionSettings(Bus, Address));

        // IRQ: falling edge means "data available, read now".
        this.gpio.OpenPin(this.irqPin, PinMode.Input);
        this.gpio.RegisterCallbackForPinValueChangedEvent(
            this.irqPin, PinEventTypes.Falling, this.OnIrq);
    }

    private void OnIrq(object sender, PinValueChangedEventArgs args) {
        try {
            // Read registers starting at 0x02 (TD_STATUS, then first point).
            //   buffer[0] = TD_STATUS       lower 4 bits = number of points
            //   buffer[1] = TOUCH1_XH       upper 2 bits = event, lower 4 bits = X[11:8]
            //   buffer[2] = TOUCH1_XL       X[7:0]
            //   buffer[3] = TOUCH1_YH       upper 4 bits = finger ID, lower 4 bits = Y[11:8]
            //   buffer[4] = TOUCH1_YL       Y[7:0]
            this.i2c.WriteRead(stackalloc byte[] { 0x02 }, this.readBuffer);

            int numPoints = this.readBuffer[0] & 0x0F;
            if (numPoints > 5) numPoints = 0;

            if (numPoints > 0) {
                int x = ((this.readBuffer[1] & 0x0F) << 8) | this.readBuffer[2];
                int y = ((this.readBuffer[3] & 0x0F) << 8) | this.readBuffer[4];
                int id = (this.readBuffer[3] >> 4) & 0x0F;
                var points = new[] { new TouchPoint(id, x, y) };
                var evt = this.lastPointCount == 0 ? TouchEventType.Pressed : TouchEventType.Moved;
                this.RaiseChanged(new TouchEventArgs(evt, points));
            }
            else if (this.lastPointCount > 0) {
                this.RaiseChanged(new TouchEventArgs(TouchEventType.Released, Array.Empty<TouchPoint>()));
            }
            this.lastPointCount = numPoints;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Ft5xx6Touch read failed: {ex.Message}");
        }
    }

    public override void Dispose() {
        if (this.disposed) return;
        this.disposed = true;
        try { this.gpio.UnregisterCallbackForPinValueChangedEvent(this.irqPin, this.OnIrq); } catch { }
        try { this.i2c.Dispose(); } catch { }
        try { I2cController.UnInitialize(Bus); } catch { }
        try { this.gpio.Dispose(); } catch { }
    }
}
