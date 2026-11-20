using System;
using System.Collections;
using System.Text;

namespace PortalDesktop.Data {
    /// <summary>
    /// A small JSON reader. Objects become Hashtables, arrays ArrayLists, numbers doubles,
    /// and true/false/null bool/null. Written by hand so numbers parse the same everywhere.
    /// </summary>
    public static class Json {
        public static object Parse(string text) {
            var i = 0;
            var value = ReadValue(text, ref i);
            return value;
        }

        // -- Lookups -----------------------------------------------------------

        public static Hashtable Obj(object o, string key) => (o as Hashtable)?[key] as Hashtable;
        public static ArrayList Arr(object o, string key) => (o as Hashtable)?[key] as ArrayList;

        public static double Num(object o, string key, double fallback) {
            var v = (o as Hashtable)?[key];
            return v is double ? (double)v : fallback;
        }

        public static bool Has(object o, string key) => (o as Hashtable)?[key] is double;

        public static string Str(object o, string key) => (o as Hashtable)?[key] as string;

        public static bool Bool(object o, string key) {
            var v = (o as Hashtable)?[key];
            return v is bool && (bool)v;
        }

        public static double NumAt(ArrayList a, int index, double fallback) =>
            a != null && index < a.Count && a[index] is double ? (double)a[index] : fallback;

        public static string StrAt(ArrayList a, int index) => a != null && index < a.Count ? a[index] as string : null;

        // -- Parser ------------------------------------------------------------

        static object ReadValue(string s, ref int i) {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw new Exception("JSON ended early");
            var c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(s, ref i);
            if (Matches(s, ref i, "true")) return true;
            if (Matches(s, ref i, "false")) return false;
            if (Matches(s, ref i, "null")) return null;
            throw new Exception("Bad JSON at " + i);
        }

        static Hashtable ReadObject(string s, ref int i) {
            var result = new Hashtable();
            i++; // {
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true) {
                SkipSpace(s, ref i);
                var key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                Expect(s, ref i, ':');
                result[key] = ReadValue(s, ref i);
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                Expect(s, ref i, '}');
                return result;
            }
        }

        static ArrayList ReadArray(string s, ref int i) {
            var result = new ArrayList();
            i++; // [
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true) {
                result.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                Expect(s, ref i, ']');
                return result;
            }
        }

        static string ReadString(string s, ref int i) {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (i < s.Length) {
                var c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                var e = s[i++];
                switch (e) {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        var code = 0;
                        for (var k = 0; k < 4 && i < s.Length; k++) code = code * 16 + HexValue(s[i++]);
                        sb.Append((char)code);
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
            throw new Exception("Unterminated JSON string");
        }

        static double ReadNumber(string s, ref int i) {
            var negative = false;
            if (s[i] == '-') { negative = true; i++; }
            double value = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') value = value * 10 + (s[i++] - '0');
            if (i < s.Length && s[i] == '.') {
                i++;
                double scale = 0.1;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') {
                    value += (s[i++] - '0') * scale;
                    scale /= 10;
                }
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E')) {
                i++;
                var expNegative = false;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) expNegative = s[i++] == '-';
                var exp = 0;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') exp = exp * 10 + (s[i++] - '0');
                for (var k = 0; k < exp; k++) value = expNegative ? value / 10 : value * 10;
            }
            return negative ? -value : value;
        }

        static int HexValue(char c) {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        static bool Matches(string s, ref int i, string word) {
            if (i + word.Length > s.Length) return false;
            for (var k = 0; k < word.Length; k++) {
                if (s[i + k] != word[k]) return false;
            }
            i += word.Length;
            return true;
        }

        static void SkipSpace(string s, ref int i) {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }

        static void Expect(string s, ref int i, char c) {
            if (i >= s.Length || s[i] != c) throw new Exception("Expected '" + c + "' in JSON at " + i);
            i++;
        }
    }
}
