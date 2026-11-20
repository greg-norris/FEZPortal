using System.Drawing;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// A full-screen on-screen keyboard with a text field above it. Three layers (letters,
    /// numbers, symbols) cover every printable ASCII character, which Wi-Fi passwords can use.
    /// Shift is one-shot, like on a phone.
    /// </summary>
    public class Keyboard {
        public const int None = 0, Changed = 1, Done = 2;

        // Layout: field on top, four rows of keys. Widths are in half keys (2 = a normal key).
        const int FieldX = Theme.Margin, FieldY = 36, FieldH = 32, ToggleW = 64;
        const int KeysY = 74, RowPitch = 49, KeyH = 45, KeyPitch = 47, KeyGap = 4;

        const int Insert = 0, Shift = 1, Backspace = 2, ToLetters = 3, ToNumbers = 4, ToSymbols = 5, Enter = 6;

        class Key {
            public string Label;    // also the text inserted, for Insert keys
            public int Action;
            public int Half;        // width in half keys
        }

        static readonly Key[][][] Layers = {
            new[] {
                Chars("qwertyuiop"),
                Chars("asdfghjkl"),
                Join(Special("shift", Shift, 3), Chars("zxcvbnm"), Special("del", Backspace, 3)),
                BottomRow("123", ToNumbers),
            },
            new[] {
                Chars("1234567890"),
                Chars("@#$%&*-+()"),
                Join(Special("#+=", ToSymbols, 3), Chars(".,!?'\":"), Special("del", Backspace, 3)),
                BottomRow("ABC", ToLetters),
            },
            new[] {
                Chars("[]{}<>^~`|"),
                Chars("\\_=;/:.,!?"),
                Join(Special("123", ToNumbers, 3), Chars("'\"@#$%&"), Special("del", Backspace, 3)),
                BottomRow("ABC", ToLetters),
            },
        };

        public string Text = "";
        public string Prompt = "";
        public string EnterLabel = "Done";
        public bool Hidden;         // show * instead of the text
        public bool AllowHide;      // offer the Show/Hide button
        public int MaxLength = 63;
        public bool Capitals;       // letters always upper case (ticker symbols); set after Reset

        int layer;
        bool shift;

        /// <summary>Starts a fresh entry.</summary>
        public void Reset(string prompt, string enterLabel, bool password, int maxLength) {
            this.Text = "";
            this.Prompt = prompt;
            this.EnterLabel = enterLabel;
            this.Hidden = password;
            this.AllowHide = password;
            this.MaxLength = maxLength;
            this.Capitals = false;
            this.layer = 0;
            this.shift = false;
        }

        // -- Drawing ---------------------------------------------------------------

        public void Draw(Canvas c) {
            this.DrawField(c);
            var rows = Layers[this.layer];
            for (var r = 0; r < rows.Length; r++) {
                var row = rows[r];
                var x = RowStart(row);
                var y = KeysY + r * RowPitch;
                foreach (var key in row) {
                    var w = key.Half * KeyPitch / 2 - KeyGap;
                    this.DrawKey(c, key, x, y, w);
                    x += key.Half * KeyPitch / 2;
                }
            }
        }

        void DrawField(Canvas c) {
            var w = this.FieldWidth;
            c.FillRoundRect(Theme.Panel, FieldX, FieldY, w, FieldH, 8);
            var inner = w - 24;
            if (this.Text.Length == 0) {
                c.DrawText(c.Ellipsize(this.Prompt, UiFont.Small, inner), UiFont.Small, Theme.TextDim, FieldX + 10, FieldY + 9);
                c.FillRect(Theme.Accent, FieldX + 8, FieldY + 7, 2, 18);
            }
            else if (this.Hidden) {
                // A dot per character (the fonts' * is tiny); the last ones if they don't all fit.
                const int dot = 13;
                var count = this.Text.Length;
                if (count > inner / dot) count = inner / dot;
                for (var i = 0; i < count; i++) c.FillCircle(Theme.White, FieldX + 16 + i * dot, FieldY + FieldH / 2, 4);
                c.FillRect(Theme.Accent, FieldX + 10 + count * dot, FieldY + 7, 2, 18);
            }
            else {
                // Show the end of the text when it doesn't fit.
                var shown = this.Text;
                while (shown.Length > 1 && c.MeasureText(shown, UiFont.Large) > inner) shown = shown.Substring(1);
                c.DrawText(shown, UiFont.Large, Theme.White, FieldX + 10, FieldY + 7);
                c.FillRect(Theme.Accent, FieldX + 11 + c.MeasureText(shown, UiFont.Large), FieldY + 7, 2, 18);
            }
            if (this.AllowHide) {
                var tx = Theme.ScreenWidth - Theme.Margin - ToggleW;
                c.FillRoundRect(Theme.PanelDark, tx, FieldY, ToggleW, FieldH, 8);
                c.DrawTextCentered(this.Hidden ? "Show" : "Hide", UiFont.Small, Theme.White, tx + ToggleW / 2, FieldY + 9);
            }
        }

        int FieldWidth => Theme.ScreenWidth - Theme.Margin * 2 - (this.AllowHide ? ToggleW + 6 : 0);

        void DrawKey(Canvas c, Key key, int x, int y, int w) {
            var bg = Theme.Panel;
            var fg = Theme.White;
            if (key.Action == Enter) { bg = Theme.Accent; fg = Theme.PanelDark; }
            else if (key.Action == Shift && this.shift) { bg = Theme.AccentSoft; fg = Theme.PanelDark; }
            else if (key.Action != Insert) bg = Theme.PanelDark;
            c.FillRoundRect(bg, x, y, w, KeyH, 8);

            var cx = x + w / 2;
            var cy = y + KeyH / 2;
            switch (key.Action) {
                case Insert:
                    if (key.Label == " ") {
                        c.DrawTextCentered("space", UiFont.Small, Theme.TextMuted, cx, cy - 8);
                    }
                    else {
                        c.DrawTextCentered(this.Shown(key.Label), UiFont.Large, fg, cx, cy - 10);
                    }
                    break;
                case Shift:
                    c.FillTriangle(fg, cx - 9, cy + 1, cx + 9, cy + 1, cx, cy - 9);
                    c.FillRect(fg, cx - 3, cy + 1, 7, 8);
                    break;
                case Backspace:
                    c.ThickLine(fg, cx - 9, cy, cx + 9, cy, 3);
                    c.ThickLine(fg, cx - 9, cy, cx - 3, cy - 6, 3);
                    c.ThickLine(fg, cx - 9, cy, cx - 3, cy + 6, 3);
                    break;
                case Enter:
                    c.DrawTextCentered(this.EnterLabel, UiFont.Large, fg, cx, cy - 10);
                    break;
                default:
                    c.DrawTextCentered(key.Label, UiFont.Small, fg, cx, cy - 8);
                    break;
            }
        }

        string Shown(string label) => (this.shift || this.Capitals) && this.layer == 0 ? label.ToUpper() : label;

        // -- Touch -----------------------------------------------------------------

        /// <summary>Handles a tap: None, Changed (redraw) or Done (the enter key).</summary>
        public int Tap(int x, int y) {
            if (y >= FieldY && y < FieldY + FieldH) {
                if (this.AllowHide && x >= Theme.ScreenWidth - Theme.Margin - ToggleW) {
                    this.Hidden = !this.Hidden;
                    return Changed;
                }
                return None;
            }
            if (y < KeysY - KeyGap / 2) return None;
            var r = (y - KeysY + KeyGap / 2) / RowPitch;
            var rows = Layers[this.layer];
            if (r >= rows.Length) return None;

            var row = rows[r];
            var kx = RowStart(row);
            foreach (var key in row) {
                var w = key.Half * KeyPitch / 2;
                if (x >= kx - KeyGap / 2 && x < kx + w - KeyGap / 2) return this.Press(key);
                kx += w;
            }
            return None;
        }

        int Press(Key key) {
            switch (key.Action) {
                case Insert:
                    if (this.Text.Length >= this.MaxLength) return None;
                    this.Text += this.Shown(key.Label);
                    this.shift = false;
                    return Changed;
                case Shift:
                    this.shift = !this.shift;
                    return Changed;
                case Backspace:
                    if (this.Text.Length == 0) return None;
                    this.Text = this.Text.Substring(0, this.Text.Length - 1);
                    return Changed;
                case ToLetters: this.layer = 0; return Changed;
                case ToNumbers: this.layer = 1; return Changed;
                case ToSymbols: this.layer = 2; return Changed;
                case Enter: return Done;
            }
            return None;
        }

        // -- Layout helpers ----------------------------------------------------------

        static int RowStart(Key[] row) {
            var half = 0;
            foreach (var key in row) half += key.Half;
            return (Theme.ScreenWidth - half * KeyPitch / 2) / 2 + KeyGap / 2;
        }

        static Key[] Chars(string chars) {
            var keys = new Key[chars.Length];
            for (var i = 0; i < chars.Length; i++) keys[i] = new Key { Label = chars.Substring(i, 1), Action = Insert, Half = 2 };
            return keys;
        }

        static Key[] Special(string label, int action, int half) => new[] { new Key { Label = label, Action = action, Half = half } };

        static Key[] BottomRow(string modeLabel, int modeAction) =>
            Join(Special(modeLabel, modeAction, 4), new[] { new Key { Label = " ", Action = Insert, Half = 10 } }, Special("enter", Enter, 6));

        static Key[] Join(Key[] a, Key[] b, Key[] c) {
            var all = new Key[a.Length + b.Length + c.Length];
            System.Array.Copy(a, 0, all, 0, a.Length);
            System.Array.Copy(b, 0, all, a.Length, b.Length);
            System.Array.Copy(c, 0, all, a.Length + b.Length, c.Length);
            return all;
        }
    }
}
