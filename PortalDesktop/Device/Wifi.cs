using System;
using System.Diagnostics;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Network;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Drivers.Microchip.Winc15x0;
using GHIElectronics.TinyCLR.Pins;
using PortalDesktop.Data;

namespace PortalDesktop.Device {
    /// <summary>
    /// The FEZ Portal's built-in ATWINC1510 Wi-Fi module (SPI3), set up as in GHI's Wi-Fi tutorial.
    /// Which network to join is decided by the data service (saved networks in Portal\wifi.txt).
    /// Only used from the data service thread.
    /// </summary>
    public class Wifi {
        readonly PortalState state;
        NetworkController controller;
        volatile bool hasAddress;

        public Wifi(PortalState state) {
            this.state = state;
        }

        public bool Connected => this.hasAddress && this.controller != null && this.controller.GetLinkConnected();

        /// <summary>Joins a network. Blocks up to about 30 seconds; returns true once there is an IP address.</summary>
        public bool Connect(string ssid, string password) {
            lock (this.state) {
                this.state.WifiSsid = ssid;
                this.state.SignalStrength = 0;
            }

            try {
                if (this.controller == null) this.Setup();
                else if (this.controller.IsEnable) this.controller.Disable();

                this.SetStatus(WifiStatus.Connecting, "Connecting to " + ssid + "...");
                this.hasAddress = false;
                this.controller.SetInterfaceSettings(new WiFiNetworkInterfaceSettings {
                    Ssid = ssid,
                    Password = password,
                    DhcpEnable = true,
                    DynamicDnsEnable = true,
                });
                this.controller.SetAsDefaultController();
                this.controller.Enable();

                for (var i = 0; i < 300 && !this.hasAddress; i++) Thread.Sleep(100);
                if (!this.hasAddress) {
                    this.SetStatus(WifiStatus.Failed, "Can't join " + ssid);
                    return false;
                }
                this.SetStatus(WifiStatus.Connected, "Connected to " + ssid);
                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine("Wi-Fi: " + ex.Message);
                this.SetStatus(WifiStatus.Failed, "Wi-Fi error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Names of the networks in range (no duplicates or blanks). The driver powers the module
        /// up for the scan if needed, but it has to know the SPI wiring first.
        /// </summary>
        public string[] Scan() {
            if (this.controller == null) this.Setup();
            var found = Winc15x0Interface.Scan();
            var list = new System.Collections.ArrayList();
            if (found != null) {
                foreach (var name in found) {
                    if (name != null && name.Length > 0 && !list.Contains(name)) list.Add(name);
                }
            }
            return (string[])list.ToArray(typeof(string));
        }

        /// <summary>Signal strength of the access point in dBm (0 if unknown).</summary>
        public void UpdateSignal() {
            try {
                var rssi = Winc15x0Interface.GetRssi();
                lock (this.state) {
                    if (rssi != this.state.SignalStrength) {
                        this.state.SignalStrength = rssi;
                        this.state.StatusVersion++;
                    }
                }
            }
            catch { }
        }

        void Setup() {
            var gpio = GpioController.GetDefault();

            var enable = gpio.OpenPin(SC20260.GpioPin.PA8);
            enable.SetDriveMode(GpioPinDriveMode.Output);
            enable.Write(GpioPinValue.High);

            var communication = new SpiNetworkCommunicationInterfaceSettings {
                SpiApiName = SC20260.SpiBus.Spi3,
                SpiSettings = new SpiConnectionSettings {
                    ChipSelectLine = gpio.OpenPin(SC20260.GpioPin.PA6),
                    ClockFrequency = 4000000,
                    Mode = SpiMode.Mode0,
                    ChipSelectType = SpiChipSelectType.Gpio,
                    ChipSelectHoldTime = TimeSpan.FromTicks(10),
                    ChipSelectSetupTime = TimeSpan.FromTicks(10),
                },
                GpioApiName = SC20260.GpioPin.Id,
                InterruptPin = gpio.OpenPin(SC20260.GpioPin.PF10),
                InterruptEdge = GpioPinEdge.FallingEdge,
                InterruptDriveMode = GpioPinDriveMode.InputPullUp,
                ResetPin = gpio.OpenPin(SC20260.GpioPin.PC3),
                ResetActiveState = GpioPinValue.Low,
            };

            this.controller = NetworkController.FromName(SC20260.NetworkController.ATWinc15x0);
            this.controller.SetCommunicationInterfaceSettings(communication);
            this.controller.NetworkAddressChanged += this.OnAddressChanged;
            this.controller.NetworkLinkConnectedChanged += this.OnLinkChanged;
        }

        void OnAddressChanged(NetworkController sender, NetworkAddressChangedEventArgs e) {
            var bytes = sender.GetIPProperties().Address.GetAddressBytes();
            var ip = bytes[0] + "." + bytes[1] + "." + bytes[2] + "." + bytes[3];
            this.hasAddress = bytes[0] != 0;
            Debug.WriteLine("Wi-Fi: address " + ip);
            lock (this.state) {
                this.state.IpAddress = this.hasAddress ? ip : "";
                this.state.StatusVersion++;
            }
        }

        void OnLinkChanged(NetworkController sender, NetworkLinkConnectedChangedEventArgs e) {
            Debug.WriteLine("Wi-Fi: link " + (e.Connected ? "up" : "down"));
            if (!e.Connected) {
                this.hasAddress = false;
                this.SetStatus(WifiStatus.Connecting, "Wi-Fi lost, reconnecting...");
            }
        }

        void SetStatus(WifiStatus status, string message) {
            lock (this.state) {
                this.state.Wifi = status;
                this.state.WifiMessage = message;
                this.state.StatusVersion++;
            }
        }
    }
}
