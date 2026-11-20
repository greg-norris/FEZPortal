using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Pins;

namespace HolidayLights.Device {
    /// <summary>
    /// The LPD8806 LED strip (GE32RGB8806A) on SPI2: clock on PI1 (SCK), data on PI3 (MOSI).
    /// The strip has no chip select and sends nothing back, so MISO and CS are left unused.
    /// The bytes come ready-made from <see cref="Lights.StripOutput"/>.
    /// </summary>
    public class LedStrip {
        /// <summary>
        /// SPI clock. The LPD8806 handles up to ~20 MHz, but 2 MHz is kinder to long, unshielded wires
        /// and still sends a 160-LED frame in about 2 ms.
        /// </summary>
        public const int ClockHz = 2000000;

        readonly SpiDevice spi;

        public LedStrip() {
            var controller = SpiController.FromName(SC20260.SpiBus.Spi2);
            this.spi = controller.GetDevice(new SpiConnectionSettings {
                ChipSelectType = SpiChipSelectType.None,
                ClockFrequency = ClockHz,       // 8-bit words are the default (DataBitLength is read-only in 3.0)
                Mode = SpiMode.Mode0,           // data is read on the rising clock edge
            });
        }

        public void Write(byte[] buffer, int length) => this.spi.Write(buffer, 0, length);
    }
}
