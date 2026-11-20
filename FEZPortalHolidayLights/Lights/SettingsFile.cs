using System.Collections;
using System.Text;

namespace HolidayLights.Lights {
    /// <summary>
    /// A "name=value" text file, one setting per line. Lines starting with # are comments.
    /// Names are not case sensitive. Set() keeps the comments and order of the original file,
    /// so the file on the SD card stays readable after the Portal saves it.
    /// </summary>
    public class SettingsFile {
        readonly ArrayList lines = new ArrayList();

        public SettingsFile(string text) {
            if (text == null) return;
            foreach (var raw in text.Split('\n')) {
                var line = raw;
                if (line.Length > 0 && line[line.Length - 1] == '\r') line = line.Substring(0, line.Length - 1);
                this.lines.Add(line);
            }
            // A trailing newline leaves one empty line at the end; drop it so saving doesn't grow the file.
            if (this.lines.Count > 0 && ((string)this.lines[this.lines.Count - 1]).Length == 0) this.lines.RemoveAt(this.lines.Count - 1);
        }

        public string Get(string name, string fallback) {
            var i = this.Find(name);
            if (i < 0) return fallback;
            var line = (string)this.lines[i];
            return line.Substring(line.IndexOf('=') + 1).Trim();
        }

        /// <summary>A whole number setting, or the fallback when it is missing or not a number.</summary>
        public int GetInt(string name, int fallback) {
            var text = this.Get(name, null);
            int value;
            return text != null && ParseInt(text, out value) ? value : fallback;
        }

        public void Set(string name, string value) {
            var i = this.Find(name);
            var line = name + "=" + value;
            if (i >= 0) this.lines[i] = line;
            else this.lines.Add(line);
        }

        public string ToText() {
            var sb = new StringBuilder();
            foreach (string line in this.lines) sb.Append(line).Append("\r\n");
            return sb.ToString();
        }

        int Find(string name) {
            name = name.ToLower();
            for (var i = 0; i < this.lines.Count; i++) {
                var line = ((string)this.lines[i]).Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(0, eq).Trim().ToLower() == name) return i;
            }
            return -1;
        }

        /// <summary>"-12" or "1500" (no int.Parse reliance on the device's mscorlib).</summary>
        public static bool ParseInt(string text, out int value) {
            value = 0;
            text = text.Trim();
            var negative = text.Length > 0 && text[0] == '-';
            var start = negative ? 1 : 0;
            if (start >= text.Length || text.Length - start > 9) return false;
            for (var i = start; i < text.Length; i++) {
                var c = text[i];
                if (c < '0' || c > '9') return false;
                value = value * 10 + (c - '0');
            }
            if (negative) value = -value;
            return true;
        }
    }
}
