using System;
using System.Collections;
using System.Drawing;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// A touch calculator: digits, + − × ÷ with the usual order of operations (2 + 3 × 4 = 14),
    /// =, clear, backspace, ± and %. The calculation so far is shown above the number.
    /// </summary>
    public class CalculatorScreen : Screen {
        // Display card and a 5 x 4 keypad.
        const int DispX = Theme.Margin, DispY = 36, DispW = Theme.ScreenWidth - Theme.Margin * 2, DispH = 60;
        const int KeysY = 102, Cols = 5, Rows = 4, KeyGap = 4, KeyH = 38;
        const int KeyW = (Theme.ScreenWidth - Theme.Margin * 2 - KeyGap * (Cols - 1)) / Cols;
        const int MaxDigits = 12;

        const string Times = "×", Divide = "÷", Minus = "−";

        static readonly string[] Keys = {
            "7", "8", "9", Divide, "C",
            "4", "5", "6", Times, "del",
            "1", "2", "3", Minus, "+/-",
            "0", ".", "%", "+", "=",
        };

        readonly ArrayList tokens = new ArrayList();    // numbers (double) and operators (string), in order
        string entry = "0";                             // the number being typed
        string history = "";                            // the calculation that gave the result shown
        bool typing;                                    // entry holds new digits (not just a result)
        bool error;

        public CalculatorScreen(Shell shell) : base(shell) { }

        public override string Title => "Calculator";

        /// <summary>The number on the display, as shown.</summary>
        public string Display => this.error ? "Error" : Grouped(this.entry);

        /// <summary>The last result, for the desktop button.</summary>
        public string LastResult => this.history.Length > 0 && !this.error ? Grouped(this.entry) : "";

        // -- Drawing -------------------------------------------------------------------

        public override void Draw(Canvas c) {
            this.DrawDisplay(c);
            for (var i = 0; i < Keys.Length; i++) this.DrawKey(c, i, KeyX(i % Cols), KeyY(i / Cols));
        }

        static int KeyX(int col) => Theme.Margin + col * (KeyW + KeyGap);
        static int KeyY(int row) => KeysY + row * (KeyH + KeyGap);

        void DrawDisplay(Canvas c) {
            Card(c, DispX, DispY, DispW, DispH);
            var right = DispX + DispW - 14;

            // The calculation so far (or the one that gave the result), small, above the number.
            var line = this.history.Length > 0 ? this.history : this.Pending();
            if (line.Length > 0) {
                while (line.Length > 1 && c.MeasureText(line, UiFont.Small) > DispW - 28) line = line.Substring(1);
                c.DrawTextRight(line, UiFont.Small, Theme.TextMuted, right, DispY + 6);
            }

            var number = this.Display;
            var font = c.MeasureText(number, UiFont.Huge) <= DispW - 28 ? UiFont.Huge : UiFont.XLarge;
            c.DrawTextRight(number, font, this.error ? Theme.Down : Theme.White, right, DispY + (font == UiFont.Huge ? 22 : 30));
        }

        void DrawKey(Canvas c, int i, int x, int y) {
            var key = Keys[i];
            Color bg, fg;
            if (key == "=") { bg = Theme.Accent; fg = Theme.PanelDark; }
            else if (IsOperator(key)) { bg = Theme.PanelLight; fg = this.PendingOperator() == key ? Theme.White : Theme.Accent; }
            else if (key.Length == 1 && (key[0] >= '0' && key[0] <= '9' || key == ".")) { bg = Theme.Panel; fg = Theme.White; }
            else { bg = Theme.PanelDark; fg = Theme.AccentSoft; }
            c.FillRoundRect(bg, x, y, KeyW, KeyH, 10);

            var cx = x + KeyW / 2;
            var cy = y + KeyH / 2;
            if (key == "del") {
                c.ThickLine(fg, cx - 9, cy, cx + 9, cy, 3);
                c.ThickLine(fg, cx - 9, cy, cx - 3, cy - 6, 3);
                c.ThickLine(fg, cx - 9, cy, cx - 3, cy + 6, 3);
                return;
            }
            var label = key == "+/-" ? "±" : key;
            var font = label.Length > 1 ? UiFont.Large : UiFont.XLarge;
            if (key == "+/-") { label = "+/" + Minus; font = UiFont.Large; }
            c.DrawTextCentered(label, font, fg, cx, cy - c.LineHeight(font) / 2);
        }

        // -- Touch -----------------------------------------------------------------------

        public override void Tap(int x, int y) {
            if (y < KeysY) return;
            var col = (x - Theme.Margin) / (KeyW + KeyGap);
            var row = (y - KeysY) / (KeyH + KeyGap);
            if (x < Theme.Margin || col >= Cols || row >= Rows) return;
            this.Press(Keys[row * Cols + col]);
            this.Shell.Invalidate();
        }

        /// <summary>Handles one key (also used by the desktop preview).</summary>
        public void Press(string key) {
            if (key == "C") { this.Clear(); return; }
            if (this.error) this.Clear();

            if (key.Length == 1 && key[0] >= '0' && key[0] <= '9') this.Digit(key);
            else if (key == ".") this.Point();
            else if (IsOperator(key)) this.Operator(key);
            else if (key == "=") this.Equals();
            else if (key == "del") this.Backspace();
            else if (key == "+/-") this.Negate();
            else if (key == "%") this.Percent();
        }

        void Clear() {
            this.tokens.Clear();
            this.entry = "0";
            this.history = "";
            this.typing = false;
            this.error = false;
        }

        void StartEntry() {
            // After "=", a digit starts a new calculation.
            if (this.history.Length > 0) {
                this.history = "";
                this.tokens.Clear();
            }
            if (!this.typing) {
                this.entry = "0";
                this.typing = true;
            }
        }

        void Digit(string d) {
            this.StartEntry();
            if (DigitCount(this.entry) >= MaxDigits) return;
            this.entry = this.entry == "0" ? d : this.entry == "-0" ? "-" + d : this.entry + d;
        }

        void Point() {
            this.StartEntry();
            if (this.entry.IndexOf('.') < 0) this.entry += ".";
        }

        void Operator(string op) {
            this.history = "";
            if (!this.typing && this.tokens.Count > 0 && this.tokens[this.tokens.Count - 1] is string) {
                this.tokens[this.tokens.Count - 1] = op;   // changed their mind: replace the operator
                return;
            }
            this.tokens.Add(Value(this.entry));
            this.tokens.Add(op);
            this.typing = false;
            // For + and −, show the running total; for × and ÷ the number stays (it binds tighter).
            double partial;
            if ((op == "+" || op == Minus) && Evaluate(this.tokens, this.tokens.Count, out partial)) this.entry = Text(partial);
            else this.entry = Text(Value(this.entry));
        }

        void Equals() {
            if (this.tokens.Count == 0) return;
            var line = this.Pending();
            var all = new ArrayList();
            foreach (var t in this.tokens) all.Add(t);
            if (this.typing || all.Count % 2 == 0) all.Add(Value(this.entry));   // "5 +" then "=": use 5 again
            double result;
            var ok = Evaluate(all, all.Count, out result);
            this.history = line + " " + Grouped(Text(Value(this.entry))) + " =";
            this.tokens.Clear();
            this.typing = false;
            if (!ok || result >= 1e15 || result <= -1e15) {
                this.error = true;
                this.entry = "0";
                return;
            }
            this.entry = Text(result);
        }

        void Backspace() {
            if (!this.typing) {
                this.history = "";
                return;
            }
            this.entry = this.entry.Length <= 1 || (this.entry.Length == 2 && this.entry[0] == '-') ? "0" : this.entry.Substring(0, this.entry.Length - 1);
        }

        void Negate() {
            if (this.history.Length > 0) { this.history = ""; this.typing = true; }
            this.entry = this.entry[0] == '-' ? this.entry.Substring(1) : "-" + this.entry;
            if (!this.typing) this.typing = true;
        }

        void Percent() {
            this.entry = Text(Value(this.entry) / 100);
            this.typing = true;
            this.history = "";
        }

        // -- Working it out --------------------------------------------------------------------

        static bool IsOperator(string key) => key == "+" || key == Minus || key == Times || key == Divide;

        string PendingOperator() => this.typing || this.tokens.Count == 0 ? null : this.tokens[this.tokens.Count - 1] as string;

        /// <summary>"12 × 3 +" from the numbers and operators entered so far.</summary>
        string Pending() {
            var line = "";
            foreach (var t in this.tokens) {
                if (line.Length > 0) line += " ";
                line += t is string ? (string)t : Grouped(Text((double)t));
            }
            return line;
        }

        /// <summary>
        /// The value of the first count tokens (a number, then operator/number pairs), with × and ÷
        /// before + and −. A trailing operator is ignored. False on division by zero.
        /// </summary>
        static bool Evaluate(ArrayList tokens, int count, out double result) {
            result = 0;
            if (count > 0 && tokens[count - 1] is string) count--;
            if (count == 0) return true;
            double sum = 0, term = (double)tokens[0];
            var sign = 1.0;
            for (var i = 1; i + 1 < count; i += 2) {
                var op = (string)tokens[i];
                var v = (double)tokens[i + 1];
                if (op == Times) {
                    term *= v;
                }
                else if (op == Divide) {
                    if (v == 0) return false;
                    term /= v;
                }
                else {
                    sum += sign * term;
                    sign = op == Minus ? -1 : 1;
                    term = v;
                }
            }
            result = sum + sign * term;
            return true;
        }

        static double Value(string text) {
            if (text == "-" || text.Length == 0) return 0;
            try {
                var v = Json.Parse(text[text.Length - 1] == '.' ? text + "0" : text);
                return v is double ? (double)v : 0;
            }
            catch {
                return 0;
            }
        }

        /// <summary>A result as text: up to 10 significant digits, no trailing zeros.</summary>
        static string Text(double v) {
            if (v == 0) return "0";
            var whole = Math.Abs(v);
            var digitsBefore = whole >= 1 ? 1 : 0;   // digits before the point (none for 0.xxx)
            for (var t = whole; t >= 10; t /= 10) digitsBefore++;
            var decimals = Math.Max(0, 10 - digitsBefore);
            var s = Format.Plain(v, decimals);
            if (s.IndexOf('.') >= 0) {
                var end = s.Length;
                while (end > 0 && s[end - 1] == '0') end--;
                if (end > 0 && s[end - 1] == '.') end--;
                s = s.Substring(0, end);
            }
            return s == "-0" ? "0" : s;
        }

        static int DigitCount(string s) {
            var n = 0;
            for (var i = 0; i < s.Length; i++) if (s[i] >= '0' && s[i] <= '9') n++;
            return n;
        }

        /// <summary>"1234567.5" -> "1,234,567.5" (the part being typed keeps its trailing "." or zeros).</summary>
        static string Grouped(string s) {
            var negative = s.Length > 0 && s[0] == '-';
            var body = negative ? s.Substring(1) : s;
            var dot = body.IndexOf('.');
            var whole = dot < 0 ? body : body.Substring(0, dot);
            var rest = dot < 0 ? "" : body.Substring(dot);
            var grouped = "";
            for (var i = 0; i < whole.Length; i++) {
                if (i > 0 && (whole.Length - i) % 3 == 0) grouped += ",";
                grouped += whole[i];
            }
            return (negative ? Minus : "") + grouped + rest;
        }
    }
}
