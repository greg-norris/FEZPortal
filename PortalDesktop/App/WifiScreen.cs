using System.Drawing;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// Wi-Fi settings, opened by tapping the Wi-Fi name or icon in the status bar: the current
    /// connection, networks in range and saved ones (tap to join, Forget to remove), an
    /// "Other network" entry for hidden networks, and the auto-join switch. Passwords are typed
    /// on the on-screen keyboard.
    /// </summary>
    public class WifiScreen : Screen {
        const int ListMode = 0, NameMode = 1, PasswordMode = 2;

        // Layout
        const int CardX = Theme.Margin, CardW = Theme.ScreenWidth - Theme.Margin * 2;
        const int StatusY = 36, StatusH = 46;
        const int ScanW = 86, ScanH = 30, ScanX = CardX + CardW - ScanW - 8, ScanY = StatusY + 8;
        const int ListY = 88, ListH = 144;
        const int RowX = CardX + 8, RowW = CardW - 16, RowsY = 114, RowH = 26, RowPitch = 29, RowsPerPage = 4;
        const int ForgetW = 62;
        const int ToggleY = 238, ToggleH = 28;

        // Kinds of list rows
        const int Current = 0, SavedInRange = 1, InRange = 2, SavedAway = 3, Other = 4;

        readonly Keyboard keyboard = new Keyboard();
        int mode, page;
        string joinSsid;
        string confirmForget;   // network whose Forget button was tapped once
        int pagerLeft, pagerMid;

        // The list as last built from the state.
        string[] rowNames = new string[0];
        int[] rowKinds = new int[0];
        int rowCount;

        public WifiScreen(Shell shell) : base(shell) { }

        public override string Title => this.mode == PasswordMode ? this.joinSsid : this.mode == NameMode ? "Other network" : "Wi-Fi";

        public override int Stamp => this.mode == ListMode ? this.State.StatusVersion + this.State.WifiVersion * 7 : 0;

        int Pages => (this.rowCount + RowsPerPage - 1) / RowsPerPage;

        public override void Opened() {
            this.mode = ListMode;
            this.page = 0;
            this.confirmForget = null;
            this.Shell.Host.ScanWifi();
        }

        public override bool Back() {
            if (this.mode == ListMode) return false;
            this.mode = ListMode;
            return true;
        }

        // -- Drawing -----------------------------------------------------------------

        public override void Draw(Canvas c) {
            if (this.mode != ListMode) {
                this.keyboard.Draw(c);
                return;
            }
            this.BuildRows();
            if (this.page >= this.Pages) this.page = 0;
            this.DrawStatus(c);
            this.DrawList(c);
            this.DrawAutoJoin(c);
        }

        void DrawStatus(Canvas c) {
            var s = this.State;
            Card(c, CardX, StatusY, CardW, StatusH);

            var connected = s.Wifi == WifiStatus.Connected;
            var bars = 0;
            if (connected) bars = s.SignalStrength == 0 || s.SignalStrength > -60 ? 3 : s.SignalStrength > -72 ? 2 : 1;
            Icons.Wifi(c, CardX + 12, StatusY + 9, bars, Theme.White, s.Wifi == WifiStatus.Failed ? Theme.Down : Theme.TextDim);

            const int textX = CardX + 48;
            var room = ScanX - textX - 8;
            string title, detail;
            if (connected) {
                title = s.WifiSsid;
                detail = "Connected";
                if (s.IpAddress.Length > 0) detail += " · " + s.IpAddress;
                if (s.SignalStrength != 0) detail += " · " + SignalWord(s.SignalStrength) + " signal";
            }
            else {
                title = s.WifiMessage;
                detail = s.Wifi == WifiStatus.Connecting ? "This takes up to 30 seconds" : "Pick a network below";
            }
            c.DrawText(c.Ellipsize(title, UiFont.Large, room), UiFont.Large, Theme.White, textX, StatusY + 5);
            c.DrawText(c.Ellipsize(detail, UiFont.Tiny, room), UiFont.Tiny, Theme.TextMuted, textX, StatusY + 27);

            c.FillRoundRect(s.WifiScanning ? Theme.PanelLight : Theme.Accent, ScanX, ScanY, ScanW, ScanH, 8);
            c.DrawTextCentered(s.WifiScanning ? "Scanning" : "Scan", UiFont.Small, s.WifiScanning ? Theme.TextMuted : Theme.PanelDark,
                ScanX + ScanW / 2, ScanY + 8);
        }

        void DrawList(Canvas c) {
            var s = this.State;
            Card(c, CardX, ListY, CardW, ListH);
            c.DrawText("Networks", UiFont.Small, Theme.White, RowX + 2, ListY + 7);
            var note = s.WifiScanning ? "Looking for networks..." : s.WifiScanMessage;
            var noteRight = CardX + CardW - 10;

            // Pager when the list doesn't fit.
            this.pagerLeft = this.pagerMid = Theme.ScreenWidth;
            if (this.Pages > 1) {
                var label = (this.page + 1) + " / " + this.Pages;
                var w = c.MeasureText(label, UiFont.Small);
                var right = CardX + CardW - 14;
                Chevron(c, right, ListY + 14, 1);
                var lx = right - 12 - w;
                c.DrawText(label, UiFont.Small, Theme.White, lx, ListY + 7);
                Chevron(c, lx - 10, ListY + 14, -1);
                this.pagerLeft = lx - 20;
                this.pagerMid = lx + w / 2;
                noteRight = this.pagerLeft - 8;
            }
            if (note.Length > 0) {
                var noteX = RowX + 2 + c.MeasureText("Networks", UiFont.Small) + 10;
                c.DrawText(c.Ellipsize(note, UiFont.Tiny, noteRight - noteX), UiFont.Tiny, Theme.TextMuted, noteX, ListY + 8);
            }

            for (var i = 0; i < RowsPerPage; i++) {
                var index = this.page * RowsPerPage + i;
                if (index >= this.rowCount) break;
                this.DrawRow(c, this.rowNames[index], this.rowKinds[index], RowsY + i * RowPitch);
            }
        }

        void DrawRow(Canvas c, string name, int kind, int y) {
            var s = this.State;
            c.FillRoundRect(Theme.PanelLight, RowX, y, RowW, RowH, 8);
            var right = RowX + RowW - 6;

            if (this.IsSaved(name, kind)) {
                var fx = RowX + RowW - ForgetW - 3;
                var sure = this.confirmForget == name;
                c.FillRoundRect(sure ? Theme.Down : Theme.PanelDark, fx, y + 3, ForgetW, RowH - 6, 6);
                c.DrawTextCentered(sure ? "Sure?" : "Forget", UiFont.Tiny, Theme.White, fx + ForgetW / 2, y + 6);
                right = fx - 8;
            }

            string tag = null;
            var tagColor = Theme.TextMuted;
            if (s.Wifi == WifiStatus.Connecting && name == s.WifiSsid && kind != Other) { tag = "Connecting..."; tagColor = Theme.Accent; }
            else if (kind == Current) { tag = "Connected"; tagColor = Theme.Up; }
            else if (kind == SavedInRange) tag = "Saved";
            else if (kind == SavedAway && s.WifiScan.Length == 0) tag = "Saved";   // nothing scanned yet
            else if (kind == SavedAway) { tag = "Not in range"; tagColor = Theme.TextDim; }
            else Icons.Forward(c, right - 6, y + RowH / 2, Theme.AccentSoft);

            if (tag != null) {
                c.DrawTextRight(tag, UiFont.Tiny, tagColor, right, y + 6);
                right -= c.MeasureText(tag, UiFont.Tiny) + 10;
            }
            else {
                right -= 18;
            }

            var color = kind == SavedAway ? Theme.TextMuted : kind == Other ? Theme.AccentSoft : Theme.White;
            c.DrawText(c.Ellipsize(name, UiFont.Medium, right - RowX - 10), UiFont.Medium, color, RowX + 10, y + 4);
        }

        void DrawAutoJoin(Canvas c) {
            Card(c, CardX, ToggleY, CardW, ToggleH);
            const int bx = CardX + 10, by = ToggleY + 6;
            if (this.State.Config.WifiAutoJoin) {
                c.FillRoundRect(Theme.Accent, bx, by, 16, 16, 4);
                c.ThickLine(Theme.PanelDark, bx + 3, by + 8, bx + 6, by + 12, 2);
                c.ThickLine(Theme.PanelDark, bx + 6, by + 12, bx + 12, by + 4, 2);
            }
            else {
                c.FillRoundRect(Theme.TextMuted, bx, by, 16, 16, 4);
                c.FillRoundRect(Theme.Panel, bx + 2, by + 2, 12, 12, 3);
            }
            c.DrawText("Auto-join saved networks when in range", UiFont.Small, Theme.White, bx + 26, ToggleY + 7);
        }

        static void Chevron(Canvas c, int tipX, int cy, int direction) {
            var backX = tipX - 4 * direction;
            c.ThickLine(Theme.AccentSoft, backX, cy - 5, tipX, cy, 2);
            c.ThickLine(Theme.AccentSoft, tipX, cy, backX, cy + 5, 2);
        }

        static string SignalWord(int rssi) => rssi > -60 ? "strong" : rssi > -72 ? "good" : "weak";

        /// <summary>Saved networks get a Forget button (the connected one may have been forgotten already).</summary>
        bool IsSaved(string name, int kind) =>
            kind == SavedInRange || kind == SavedAway || (kind == Current && this.State.Config.FindWifi(name) != null);

        // -- The list ----------------------------------------------------------------

        /// <summary>
        /// The connected network, saved networks in range, other networks in range, saved
        /// networks not seen, then "Other network...".
        /// </summary>
        void BuildRows() {
            var s = this.State;
            var saved = s.Config.WifiNetworks;
            var scan = s.WifiScan;
            var size = scan.Length + saved.Count + 2;
            this.rowNames = new string[size];
            this.rowKinds = new int[size];
            this.rowCount = 0;

            var current = s.Wifi == WifiStatus.Connected ? s.WifiSsid : null;
            if (current != null && current.Length > 0) this.AddRow(current, Current);
            foreach (var name in scan) {
                if (name != current && s.Config.FindWifi(name) != null) this.AddRow(name, SavedInRange);
            }
            foreach (var name in scan) {
                if (name != current && s.Config.FindWifi(name) == null) this.AddRow(name, InRange);
            }
            foreach (WifiNetwork n in saved) {
                if (n.Ssid != current && !Contains(scan, n.Ssid)) this.AddRow(n.Ssid, SavedAway);
            }
            this.AddRow("Other network...", Other);
        }

        void AddRow(string name, int kind) {
            this.rowNames[this.rowCount] = name;
            this.rowKinds[this.rowCount] = kind;
            this.rowCount++;
        }

        static bool Contains(string[] names, string name) {
            foreach (var n in names) {
                if (n == name) return true;
            }
            return false;
        }

        // -- Touch -------------------------------------------------------------------

        public override void Tap(int x, int y) {
            if (this.mode != ListMode) {
                this.KeyboardTap(x, y);
                return;
            }

            var s = this.State;
            if (Inside(x, y, ScanX - 4, ScanY - 4, ScanW + 8, ScanH + 8)) {
                if (!s.WifiScanning) this.Shell.Host.ScanWifi();
                this.Shell.Invalidate();
                return;
            }

            if (Inside(x, y, CardX, ToggleY, CardW, ToggleH)) {
                s.Config.WifiAutoJoin = !s.Config.WifiAutoJoin;
                this.Shell.Host.SaveSettings(DefaultFiles.WifiName, s.Config.WifiText());
                s.WifiVersion++;
                return;
            }

            if (y >= ListY && y < RowsY && x >= this.pagerLeft) {
                this.Swipe(x < this.pagerMid ? -1 : 1);
                return;
            }

            if (y < RowsY || y >= RowsY + RowsPerPage * RowPitch || x < RowX || x >= RowX + RowW) return;
            var index = this.page * RowsPerPage + (y - RowsY) / RowPitch;
            if (index >= this.rowCount) return;
            var name = this.rowNames[index];
            var kind = this.rowKinds[index];

            // Forget takes two taps.
            if (this.IsSaved(name, kind) && x >= RowX + RowW - ForgetW - 6) {
                if (this.confirmForget == name) {
                    this.confirmForget = null;
                    s.Config.ForgetWifi(name);
                    this.Shell.Host.SaveSettings(DefaultFiles.WifiName, s.Config.WifiText());
                    s.WifiVersion++;
                }
                else {
                    this.confirmForget = name;
                }
                this.Shell.Invalidate();
                return;
            }
            this.confirmForget = null;

            switch (kind) {
                case SavedInRange:
                case SavedAway:
                    this.Shell.Host.JoinWifi(name, s.Config.FindWifi(name).Password);
                    break;
                case InRange:
                    this.AskPassword(name);
                    break;
                case Other:
                    this.mode = NameMode;
                    this.keyboard.Reset("Network name (SSID)", "Next", false, 32);
                    break;
            }
            this.Shell.Invalidate();
        }

        void AskPassword(string ssid) {
            this.joinSsid = ssid;
            this.mode = PasswordMode;
            this.keyboard.Reset("Password (leave empty for an open network)", "Join", true, 63);
        }

        void KeyboardTap(int x, int y) {
            var result = this.keyboard.Tap(x, y);
            if (result == Keyboard.None) return;
            if (result == Keyboard.Done) {
                var text = this.keyboard.Text;
                if (this.mode == NameMode) {
                    if (text.Length == 0) return;
                    this.AskPassword(text);
                }
                else {
                    this.Shell.Host.JoinWifi(this.joinSsid, text);
                    this.mode = ListMode;
                }
            }
            this.Shell.Invalidate();
        }

        public override void Swipe(int direction) {
            if (this.mode != ListMode || this.Pages <= 1) return;
            this.page = (this.page + direction + this.Pages) % this.Pages;
            this.Shell.Invalidate();
        }
    }
}
