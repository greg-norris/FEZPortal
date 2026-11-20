using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using GHIElectronics.TinyCLR.Devices.Storage;
using GHIElectronics.TinyCLR.IO;
using GHIElectronics.TinyCLR.Pins;
using PortalDesktop.Data;

namespace PortalDesktop.Device {
    /// <summary>
    /// The SD card. Settings live in \Portal\*.txt; data the Portal learns (company names,
    /// location) is cached in \Portal\cache\. Without a card the built-in defaults are used.
    /// </summary>
    public class Storage {
        readonly object sync = new object();
        StorageController controller;
        string root;    // e.g. "A:\Portal\"

        public bool Present => this.root != null;

        /// <summary>Mounts the card, writes any missing settings files, and loads the settings into state.</summary>
        public void Load(PortalState state) {
            var config = Config.Defaults();
            try {
                this.controller = StorageController.FromName(SC20260.StorageController.SdCard);
                var drive = FileSystem.Mount(this.controller.Hdc);
                this.root = drive.Name + DefaultFiles.Folder + "\\";
                if (!Directory.Exists(this.root)) Directory.CreateDirectory(this.root);
                if (!Directory.Exists(this.root + "cache")) Directory.CreateDirectory(this.root + "cache");

                var created = 0;
                for (var i = 0; i < DefaultFiles.Names.Length; i++) {
                    var name = DefaultFiles.Names[i];
                    var text = this.ReadText(name);
                    if (text == null) {
                        this.WriteText(name, DefaultFiles.Contents[i]);
                        text = DefaultFiles.Contents[i];
                        created++;
                    }
                    config.Apply(name, text);
                }
                lock (state) {
                    state.SdPresent = true;
                    state.SdMessage = created > 0 ? "SD card ready (created " + created + " settings files)" : "SD card ready";
                }
                Debug.WriteLine("SD: " + this.root + ", " + created + " files created");
            }
            catch (Exception ex) {
                this.root = null;
                lock (state) {
                    state.SdPresent = false;
                    state.SdMessage = "No SD card (using built-in settings)";
                }
                Debug.WriteLine("SD: not available: " + ex.Message);
            }

            lock (state) {
                state.Config = config;
                state.SetStocksFromConfig();
                state.StatusVersion++;
                state.CalendarVersion++;
            }
        }

        /// <summary>Reads a file in the Portal folder (e.g. "wifi.txt" or "cache\\names.txt"); null if missing.</summary>
        public string ReadText(string name) {
            if (this.root == null) return null;
            lock (this.sync) {
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
        }

        public bool WriteText(string name, string text) {
            if (this.root == null) return false;
            lock (this.sync) {
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
}
