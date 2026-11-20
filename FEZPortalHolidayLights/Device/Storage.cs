using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using GHIElectronics.TinyCLR.Devices.Storage;
using GHIElectronics.TinyCLR.IO;
using GHIElectronics.TinyCLR.Pins;

namespace HolidayLights.Device {
    /// <summary>
    /// The SD card: settings live in \HolidayLights\lights.txt. Without a card the lights still
    /// work, with the built-in settings, but changes are forgotten at power-off.
    /// </summary>
    public class Storage {
        public const string Folder = "HolidayLights";

        StorageController controller;
        string root;    // e.g. "A:\HolidayLights\"

        public bool Present => this.root != null;

        public void Mount() {
            try {
                this.controller = StorageController.FromName(SC20260.StorageController.SdCard);
                var drive = FileSystem.Mount(this.controller.Hdc);
                this.root = drive.Name + Folder + "\\";
                if (!Directory.Exists(this.root)) Directory.CreateDirectory(this.root);
                Debug.WriteLine("SD: " + this.root);
            }
            catch (Exception ex) {
                this.root = null;
                Debug.WriteLine("SD: not available: " + ex.Message);
            }
        }

        /// <summary>A file in the HolidayLights folder, or null if it (or the card) is missing.</summary>
        public string ReadText(string name) {
            if (this.root == null) return null;
            try {
                var path = this.root + name;
                if (!File.Exists(path)) return null;
                var bytes = File.ReadAllBytes(path);
                var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;   // skip a UTF-8 BOM
                return new string(Encoding.UTF8.GetChars(bytes, start, bytes.Length - start));
            }
            catch (Exception ex) {
                Debug.WriteLine("SD: can't read " + name + ": " + ex.Message);
                return null;
            }
        }

        public bool WriteText(string name, string text) {
            if (this.root == null) return false;
            try {
                var path = this.root + name;
                if (File.Exists(path)) File.Delete(path);   // WriteAllBytes doesn't shorten an existing file everywhere
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
                FileSystem.Flush(this.controller.Hdc);
                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine("SD: can't write " + name + ": " + ex.Message);
                return false;
            }
        }
    }
}
