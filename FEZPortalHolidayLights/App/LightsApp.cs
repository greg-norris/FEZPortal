using System;
using System.Drawing;
using HolidayLights.Lights;
using HolidayLights.Ui;

namespace HolidayLights.App {
    /// <summary>Where the settings are kept (the SD card on the Portal, a stand-in in the preview).</summary>
    public interface ISettingsStore {
        bool Present { get; }

        /// <summary>Writes lights.txt; false when there is nowhere to write it.</summary>
        bool Save(string text);
    }

    /// <summary>
    /// The UI: owns the pages, routes touches, draws the live picture of the strip under the status
    /// bar on every page, and saves the settings a couple of seconds after the last change (so
    /// dragging a slider doesn't write the SD card dozens of times).
    /// </summary>
    public class LightsApp {
        public const int StripY = 33, StripH = 14;
        const int StripFrameMs = 125;   // the picture of the strip: 8 a second for 160 LEDs, 4 for 320, leaves time for touch
        const int RowLeds = 160;
        const int SaveDelaySeconds = 2;

        public readonly LightSettings Settings;
        public readonly LightEngine Engine;
        public readonly ISettingsStore Store;
        readonly Canvas canvas;
        readonly int[] stripColors;

        public readonly MainPage Main;
        public readonly SetupPage Setup;
        public readonly HolidaysPage HolidayMenu;
        public readonly ThemesPage ThemeMenu;

        Page current;
        bool dirty = true;
        int savedVersion;
        DateTime saveDue = DateTime.MinValue, nextStrip = DateTime.MinValue;

        public LightsApp(LightSettings settings, LightEngine engine, ISettingsStore store, Canvas canvas) {
            this.Settings = settings;
            this.Engine = engine;
            this.Store = store;
            this.canvas = canvas;
            this.stripColors = new int[LightSettings.MaxCount];
            this.savedVersion = settings.Version;
            this.Main = new MainPage(this);
            this.Setup = new SetupPage(this);
            this.HolidayMenu = new HolidaysPage(this);
            this.ThemeMenu = new ThemesPage(this);
            this.current = this.Main;
        }

        public Page Current => this.current;

        public void Show(Page page) {
            this.current = page;
            page.Opened();
            this.dirty = true;
        }

        public void Invalidate() => this.dirty = true;

        /// <summary>A touch event from <see cref="Device.TouchInput"/>: kind 0 down, 1 move, 2 up.</summary>
        public void Touch(int kind, int x, int y) {
            // Still touching: wait a little longer before saving.
            if (this.saveDue != DateTime.MinValue) this.saveDue = DateTime.UtcNow.AddSeconds(SaveDelaySeconds);
            if (kind == 0) this.current.Down(x, y);
            else if (kind == 1) this.current.Move(x, y);
            else this.current.Up(x, y);
        }

        /// <summary>Redraws what changed and saves pending settings. Call every 20-30 ms.</summary>
        public void Update(DateTime utcNow) {
            var c = this.canvas;
            if (this.dirty) {
                c.FillRect(Theme.Background, 0, 0, Theme.ScreenWidth, Theme.ScreenHeight);
                this.current.Draw(c);
                this.DrawStrip(c);
                c.Flush();
                this.dirty = false;
                this.nextStrip = utcNow.AddMilliseconds(StripFrameMs);
            }
            else {
                this.current.Frame(c);
                if (utcNow >= this.nextStrip) {
                    this.DrawStrip(c);
                    c.Flush(0, StripY, Theme.ScreenWidth, StripH);
                    this.nextStrip = utcNow.AddMilliseconds(StripFrameMs * ((this.Engine.Count + RowLeds - 1) / RowLeds));
                }
            }
            this.SaveWhenSettled(utcNow);
        }

        void SaveWhenSettled(DateTime utcNow) {
            var version = this.Settings.Version;
            if (version == this.savedVersion) return;
            if (this.saveDue == DateTime.MinValue) {
                this.saveDue = utcNow.AddSeconds(SaveDelaySeconds);
                return;
            }
            if (utcNow < this.saveDue) return;
            string text;
            lock (this.Settings) text = this.Settings.ToText();
            this.Store.Save(text);
            this.savedVersion = version;
            this.saveDue = DateTime.MinValue;
        }

        /// <summary>
        /// A dot per LED along the top of the screen, showing what the strip shows: one row for up to
        /// 160 LEDs, more rows (one per 160) for longer strips, so the dots stay apart.
        /// </summary>
        void DrawStrip(Canvas c) {
            var count = this.Engine.CopyShown(this.stripColors);
            c.FillRect(Theme.PanelDark, 0, StripY, Theme.ScreenWidth, StripH);
            if (count <= 0) return;
            var rows = (count + RowLeds - 1) / RowLeds;
            var perRow = (count + rows - 1) / rows;
            const int inner = StripH - 4;
            var rowH = (inner - 2 * (rows - 1)) / rows;
            if (rowH < 1) rowH = 1;
            var w = Theme.ScreenWidth / perRow - 1;
            if (w < 1) w = 1;
            var off = Color.FromArgb(32, 44, 84);
            var colors = this.stripColors;
            var lift = Lift;
            var y = StripY + 2;
            var col = 0;
            for (var i = 0; i < count; i++) {
                var rgb = colors[i];
                // Lift dim LEDs a little so the pattern stays visible on the screen.
                var color = rgb == 0 ? off : Color.FromArgb(lift[(rgb >> 16) & 0xFF], lift[(rgb >> 8) & 0xFF], lift[rgb & 0xFF]);
                c.FillRect(color, col * Theme.ScreenWidth / perRow, y, w, rowH);
                if (++col == perRow) {
                    col = 0;
                    y += rowH + 2;
                }
            }
        }

        // Channel level -> level drawn on the screen (dim channels lifted).
        static readonly byte[] Lift = MakeLift();

        static byte[] MakeLift() {
            var table = new byte[256];
            for (var v = 1; v < 256; v++) table[v] = (byte)(40 + v * 215 / 255);
            return table;
        }
    }
}
