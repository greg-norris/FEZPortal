using System.Collections;
using System.Text;

namespace PortalDesktop.Data {
    /// <summary>
    /// A "name=value" text file, one setting per line. Lines starting with # are comments.
    /// Names are not case sensitive. Set() keeps the comments and order of the original file,
    /// so the files on the SD card stay readable after the Portal changes a setting.
    /// </summary>
    public class SettingsFile {
        readonly ArrayList lines = new ArrayList();

        public SettingsFile() { }

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

        public double GetNumber(string name, double fallback) {
            var text = this.Get(name, null);
            if (text == null || text.Length == 0) return fallback;
            try {
                var value = Json.Parse(text);
                return value is double ? (double)value : fallback;
            }
            catch {
                return fallback;
            }
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
    }
}
