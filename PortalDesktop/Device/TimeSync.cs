using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Rtc;
using GHIElectronics.TinyCLR.Native;
using PortalDesktop.Data;

namespace PortalDesktop.Device {
    /// <summary>
    /// Keeps the system clock (UTC) right: from the battery-backed RTC at startup, then from an
    /// internet time server (SNTP) once Wi-Fi is up. The RTC is updated after each sync.
    /// </summary>
    public static class TimeSync {
        /// <summary>Loads the time from the RTC if it has a valid one. Returns false if it doesn't.</summary>
        public static bool LoadFromRtc() {
            try {
                var rtc = RtcController.GetDefault();
                if (!rtc.IsValid) return false;
                var now = rtc.Now;
                if (now.Year < 2024) return false;
                SystemTime.SetTime(now);
                Debug.WriteLine("Time: from RTC " + now);
                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine("Time: RTC not available: " + ex.Message);
                return false;
            }
        }

        /// <summary>Asks the time server and sets the system clock and RTC. Throws on failure.</summary>
        public static DateTime SyncFromNetwork(string server) {
            var utc = QuerySntp(server);
            SystemTime.SetTime(utc);
            try {
                var rtc = RtcController.GetDefault();
                rtc.Now = utc;
            }
            catch (Exception ex) {
                Debug.WriteLine("Time: can't set RTC: " + ex.Message);
            }
            Debug.WriteLine("Time: synced from " + server + ": " + utc);
            return utc;
        }

        static DateTime QuerySntp(string server) {
            var data = new byte[48];
            data[0] = 0x1B;   // no leap warning, version 3, client mode

            var addresses = Dns.GetHostEntry(server).AddressList;
            if (addresses.Length == 0) throw new Exception("Time server not found");
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
                socket.ReceiveTimeout = 5000;
                socket.SendTimeout = 5000;
                socket.Connect(new IPEndPoint(addresses[0], 123));
                Thread.Sleep(1);
                socket.Send(data);
                var read = socket.Receive(data);
                if (read < 48) throw new Exception("Short answer from time server");
            }

            ulong seconds = (ulong)data[40] << 24 | (ulong)data[41] << 16 | (ulong)data[42] << 8 | data[43];
            ulong fraction = (ulong)data[44] << 24 | (ulong)data[45] << 16 | (ulong)data[46] << 8 | data[47];
            var milliseconds = seconds * 1000 + fraction * 1000 / 0x100000000UL;
            var utc = new DateTime(1900, 1, 1).AddMilliseconds(milliseconds);
            if (utc.Year < 2024) throw new Exception("Time server sent a bad time");
            return utc;
        }
    }
}
