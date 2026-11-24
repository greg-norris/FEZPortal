using System;
using System.Collections;
using System.Text;

namespace PortalDesktop.Data {
    /// <summary>One headline from newsdata.io.</summary>
    public class NewsItem {
        public string Title = "";
        public string Description = "";    // the summary (may be empty)
        public string Source = "";         // e.g. "Billboard"
        public DateTime Published = DateTime.MinValue;   // UTC, MinValue = unknown
    }

    /// <summary>
    /// Latest headlines from newsdata.io (free plan: 200 requests a day, 10 headlines each).
    /// Fields the Portal can't show are left out of the answer to keep it small (~10 KB), and the
    /// text is cleaned up to the characters the Portal's fonts have.
    /// </summary>
    public static class NewsData {
        /// <summary>Read at most this much of an answer (a longer one is cut after the last whole headline).</summary>
        public const int MaxBytes = 24576;

        /// <summary>Summaries are cut to this many characters (one feed sends whole articles).</summary>
        public const int MaxDescription = 1200;

        const string ExcludedFields =
            "content,keywords,creator,image_url,video_url,sentiment,sentiment_stats,ai_tag,ai_region,ai_org,ai_summary," +
            "source_icon,source_priority,source_url,fetched_at,datatype,language,country,duplicate,category,link,pubDateTZ";

        public static string Url(Config config) {
            var url = "https://newsdata.io/api/1/latest?apikey=" + config.NewsKey;
            if (config.NewsCountry.Length > 0) url += "&country=" + config.NewsCountry;
            if (config.NewsLanguage.Length > 0) url += "&language=" + config.NewsLanguage;
            if (config.NewsCategory.Length > 0) url += "&category=" + config.NewsCategory;
            if (config.NewsTopSources) url += "&prioritydomain=top";
            return url + "&removeduplicate=1&excludefield=" + ExcludedFields;
        }

        public static NewsItem[] Parse(string json) {
            object root;
            try {
                root = Json.Parse(json);
            }
            catch (Exception) {
                // Cut off at MaxBytes: keep the headlines that arrived whole.
                var cut = json.LastIndexOf("},{\"article_id\"");
                if (cut < 0) throw;
                root = Json.Parse(json.Substring(0, cut + 1) + "]}");
            }
            if (Json.Str(root, "status") != "success") throw new Exception(ErrorMessage(root) ?? "News service error");

            var results = Json.Arr(root, "results");
            var list = new ArrayList();
            if (results != null) {
                foreach (var r in results) {
                    var title = Clean(Json.Str(r, "title"), 300);
                    if (title.Length == 0) continue;
                    var item = new NewsItem { Title = title };
                    var description = Clean(Json.Str(r, "description"), MaxDescription);
                    // Some feeds repeat the headline as the summary.
                    item.Description = description == title ? "" : description;
                    item.Source = SourceName(Json.Str(r, "source_name"), Json.Str(r, "source_id"));
                    // pubDate is "2026-11-24 00:16:00" in UTC.
                    Clock.TryParseIso(Json.Str(r, "pubDate"), out item.Published);
                    list.Add(item);
                }
            }
            return (NewsItem[])list.ToArray(typeof(NewsItem));
        }

        /// <summary>The message in an error answer ({"status":"error","results":{"message":...}}), or null.</summary>
        public static string ErrorMessage(string body) {
            if (body == null || body.Length == 0) return null;
            try {
                return ErrorMessage(Json.Parse(body));
            }
            catch (Exception) {
                return null;
            }
        }

        static string ErrorMessage(object root) {
            var message = Json.Str(Json.Obj(root, "results"), "message");
            return message != null ? Clean(message, 120) : null;
        }

        /// <summary>A few sources come with their web address as name ("Https://www.kxii.com"): show "kxii.com".</summary>
        static string SourceName(string name, string id) {
            name = Clean(name, 60);
            if (name.Length == 0) return Clean(id, 40);
            if (name.ToLower().IndexOf("http") != 0) return name;
            var start = name.IndexOf("://");
            start = start < 0 ? 0 : start + 3;
            if (name.ToLower().IndexOf("www.", start) == start) start += 4;
            var end = name.IndexOf('/', start);
            return end < 0 ? name.Substring(start) : name.Substring(start, end - start);
        }

        /// <summary>"Just now", "25 min ago", "3 hr ago", "Yesterday", "4 days ago"; "" when unknown.</summary>
        public static string Age(DateTime publishedUtc, DateTime nowUtc) {
            if (publishedUtc == DateTime.MinValue) return "";
            var minutes = (nowUtc - publishedUtc).Ticks / TimeSpan.TicksPerMinute;
            if (minutes < 1) return "Just now";
            if (minutes < 60) return minutes + " min ago";
            if (minutes < 24 * 60) return minutes / 60 + " hr ago";
            if (minutes < 48 * 60) return "Yesterday";
            return minutes / (24 * 60) + " days ago";
        }

        // -- Text clean-up --------------------------------------------------------------

        // Accented letters U+00C0..U+00FF without their accents (× and ÷ stay: the fonts have them).
        const string Latin1 = "AAAAAAACEEEEIIIIDNOOOOO×OUUUUYTsaaaaaaaceeeeiiiidnooooo÷ouuuuyty";

        /// <summary>
        /// Text the fonts can draw: HTML tags and entities removed, curly quotes and dashes made
        /// plain, accents dropped, runs of spaces and line breaks turned into one space, and cut
        /// to maxLength characters (with "...").
        /// </summary>
        public static string Clean(string s, int maxLength) {
            if (s == null) return "";
            var sb = new StringBuilder();
            var space = true;       // the last character written was a space (or nothing yet)
            var cut = false;        // stopped before the end of s
            // Indexed loop: foreach over a string doesn't compile against the TinyCLR mscorlib (CS7038).
            for (var i = 0; i < s.Length; i++) {
                if (sb.Length > maxLength) {
                    cut = true;
                    break;
                }
                var c = s[i];
                if (c == '<' && i + 1 < s.Length && (IsLetter(s[i + 1]) || s[i + 1] == '/')) {
                    var close = s.IndexOf('>', i);
                    if (close > 0) {
                        i = close;
                        c = ' ';
                    }
                }
                else if (c == '&') {
                    int length;
                    var decoded = Entity(s, i, out length);
                    if (length > 0) {
                        c = decoded;
                        i += length - 1;
                    }
                }

                if (c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == ' ' || (c >= ' ' && c <= ' ') || c == ' ') {
                    if (!space) sb.Append(' ');
                    space = true;
                    continue;
                }
                if (c >= ' ' && c <= '~') sb.Append(c);
                else if (c >= 'À' && c <= 'ÿ') sb.Append(Latin1[c - 'À']);
                else if (c == '‘' || c == '’' || c == '‚' || c == '′' || c == '´') sb.Append('\'');
                else if (c == '“' || c == '”' || c == '„' || c == '″' || c == '«' || c == '»') sb.Append('"');
                else if (c >= '‐' && c <= '–') sb.Append('-');
                else if (c == '—' || c == '―') sb.Append('—');
                else if (c == '…') sb.Append("...");
                else if (c == '°' || c == '·' || c == '−') sb.Append(c);
                else if (c == '•') sb.Append('·');
                else continue;      // a character the fonts don't have (emoji, other scripts...)
                space = false;
            }
            var text = sb.ToString().Trim();
            if (!cut && text.Length <= maxLength) return text;
            // Cut at a word if there is one nearby.
            var limit = text.Length < maxLength ? text.Length : maxLength;
            var at = text.LastIndexOf(' ', limit - 1);
            if (at < limit - 30 || at <= 0) at = limit - 3;
            return text.Substring(0, at).TrimEnd() + "...";
        }

        static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        /// <summary>The character of the HTML entity at s[i] ("&amp;", "&#8217;", "&#x2019;"...); length 0 if there is none.</summary>
        static char Entity(string s, int i, out int length) {
            length = 0;
            var end = s.IndexOf(';', i);
            if (end < 0 || end - i > 9) return '&';
            var name = s.Substring(i + 1, end - i - 1);
            char c;
            if (name.Length > 1 && name[0] == '#') {
                var hex = name[1] == 'x' || name[1] == 'X';
                var code = 0;
                for (var k = hex ? 2 : 1; k < name.Length; k++) {
                    var d = name[k];
                    int v;
                    if (d >= '0' && d <= '9') v = d - '0';
                    else if (hex && d >= 'a' && d <= 'f') v = d - 'a' + 10;
                    else if (hex && d >= 'A' && d <= 'F') v = d - 'A' + 10;
                    else return '&';
                    code = code * (hex ? 16 : 10) + v;
                    if (code > 0xFFFF) return '&';
                }
                c = (char)code;
            }
            else if (name == "amp") c = '&';
            else if (name == "quot") c = '"';
            else if (name == "apos") c = '\'';
            else if (name == "lt") c = '<';
            else if (name == "gt") c = '>';
            else if (name == "nbsp") c = ' ';
            else if (name == "rsquo" || name == "lsquo") c = '\'';
            else if (name == "rdquo" || name == "ldquo") c = '"';
            else if (name == "ndash") c = '-';
            else if (name == "mdash") c = '—';
            else if (name == "hellip") c = '…';
            else return '&';
            length = end - i + 1;
            return c;
        }
    }
}
