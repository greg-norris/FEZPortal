using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PortalDesktop.Device {
    /// <summary>
    /// Simple HTTP(S) GET returning the body as text. HTTPS checks the server against the root
    /// certificates built into the program: GTS Root R4 and GlobalSign Root CA (finnhub.io's
    /// chain ends in both), DigiCert Global Root G2 (outlook.office365.com, for calendars) and
    /// Go Daddy Root G2 (newsdata.io, for the news).
    /// TLS also needs the right date, so set the clock first.
    ///
    /// TinyCLR's HttpWebRequest passes only the FIRST certificate of HttpsAuthentCerts to TLS, so
    /// each request gets a single root: the one that worked for that host before, else a guess,
    /// then the others in turn until the handshake succeeds.
    ///
    /// Memory matters here: every HTTPS connection needs a 16 KB block for TLS, so answers are read
    /// into one receive buffer that is kept between requests (instead of a new, growing one each
    /// time) and decoded straight to a string. Only the data service thread makes requests.
    /// </summary>
    public static class Http {
        static X509Certificate[][] roots;                                   // one-certificate arrays
        static readonly System.Collections.Hashtable hostRoots = new System.Collections.Hashtable();   // host -> index that worked
        static byte[] buffer = new byte[8192];
        static readonly object sync = new object();

        public static string Get(string url) => Get(url, int.MaxValue, null);

        /// <summary>
        /// Like Get, but stops reading after maxBytes, or as soon as the text stopAt has arrived
        /// (for answers where only the start matters).
        /// </summary>
        public static string Get(string url, int maxBytes, string stopAt) {
            lock (sync) return GetLocked(url, maxBytes, stopAt);
        }

        /// <summary>
        /// Downloads a text file and hands it to the sink one line at a time, so even a large
        /// answer (an Outlook calendar is ~200 KB) never has to fit in memory. Folded lines the
        /// sink doesn't want (sink.SkipFolded) are skipped without being decoded.
        /// </summary>
        public static void GetLines(string url, PortalDesktop.Data.LineSink sink) {
            lock (sync) {
                HttpWebRequest request;
                using (var response = Send(url, out request)) {
                    try {
                        var status = (int)response.StatusCode;
                        if (status < 200 || status > 299) throw new HttpException(status, ReadAll(response, 2048, null));
                        using (var stream = response.GetResponseStream()) ReadLines(stream, sink);
                    }
                    finally {
                        request.Dispose();
                    }
                }
            }
        }

        static byte[] lineBuffer = new byte[256];

        static void ReadLines(System.IO.Stream stream, PortalDesktop.Data.LineSink sink) {
            var length = 0;             // bytes of the current line
            var skipping = false;       // current line is a folded line the sink doesn't want
            var lineStart = true;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) {
                for (var i = 0; i < read; i++) {
                    var b = buffer[i];
                    if (b == (byte)'\n') {
                        if (!skipping) sink.Line(length > 0 ? Encoding.UTF8.GetString(lineBuffer, 0, length) : "");
                        length = 0;
                        skipping = false;
                        lineStart = true;
                        continue;
                    }
                    if (lineStart) {
                        lineStart = false;
                        skipping = (b == (byte)' ' || b == (byte)'\t') && sink.SkipFolded;
                    }
                    if (skipping || b == (byte)'\r') continue;
                    if (length == lineBuffer.Length) {
                        if (length >= 16384) continue;   // absurdly long line: keep the start only
                        var bigger = new byte[length * 2];
                        Array.Copy(lineBuffer, bigger, length);
                        lineBuffer = bigger;
                    }
                    lineBuffer[length++] = b;
                }
            }
            if (length > 0 && !skipping) sink.Line(Encoding.UTF8.GetString(lineBuffer, 0, length));
        }

        static string GetLocked(string url, int maxBytes, string stopAt) {
            HttpWebRequest request;
            using (var response = Send(url, out request)) {
                try {
                    var status = (int)response.StatusCode;
                    var body = ReadAll(response, maxBytes, stopAt);
                    if (status < 200 || status > 299) throw new HttpException(status, body);
                    return body;
                }
                finally {
                    request.Dispose();
                }
            }
        }

        static HttpWebRequest Create(string url, X509Certificate[] root) {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.KeepAlive = false;
            request.Timeout = 20000;
            request.ReadWriteTimeout = 15000;
            request.UserAgent = "PortalDesktop/1.0";
            if (root != null) request.HttpsAuthentCerts = root;
            return request;
        }

        /// <summary>
        /// Sends the request and returns the response (the caller disposes it and the request).
        /// For HTTPS, finds the root certificate the host's chain ends in (see the class notes).
        /// </summary>
        static HttpWebResponse Send(string url, out HttpWebRequest request) {
            if (url.IndexOf("https:") != 0) {
                request = Create(url, null);
                return (HttpWebResponse)request.GetResponse();
            }

            var all = Roots();
            var host = HostOf(url);
            var known = hostRoots[host];
            var first = known != null ? (int)known : GuessRoot(host);
            Exception failure = null;
            for (var i = 0; i < all.Length; i++) {
                var index = (first + i) % all.Length;
                request = Create(url, all[index]);
                try {
                    var response = (HttpWebResponse)request.GetResponse();
                    if (known == null) {
                        hostRoots[host] = index;
                        System.Diagnostics.Debug.WriteLine("Http: " + host + " uses root certificate " + index);
                    }
                    return response;
                }
                catch (WebException ex) {
                    request.Dispose();
                    failure = ex;
                    // An HTTP answer, or a host whose root is already known: not a certificate problem.
                    if (ex.Response != null || known != null) break;
                    System.Diagnostics.Debug.WriteLine("Http: " + host + " didn't connect with root certificate " + index + ", trying the next");
                }
            }
            request = null;
            throw failure;
        }

        /// <summary>Index into Roots() to try first for a host.</summary>
        static int GuessRoot(string host) {
            if (host.IndexOf("office365.com") >= 0 || host.IndexOf("outlook.com") >= 0 || host.IndexOf("office.com") >= 0 || host.IndexOf("live.com") >= 0) return 2;
            if (host.IndexOf("newsdata.io") >= 0) return 3;
            return 0;
        }

        static string HostOf(string url) {
            var start = url.IndexOf("://");
            start = start < 0 ? 0 : start + 3;
            var end = start;
            while (end < url.Length && url[end] != '/' && url[end] != ':' && url[end] != '?') end++;
            return url.Substring(start, end - start).ToLower();
        }

        static string ReadAll(HttpWebResponse response, int maxBytes, string stopAt) {
            var expected = response.ContentLength;
            if (expected < 0 || expected > maxBytes) expected = -1;
            var limit = expected > 0 ? (int)expected : maxBytes;
            var total = 0;
            using (var stream = response.GetResponseStream()) {
                while (total < limit) {
                    if (total == buffer.Length) {
                        // Grow the shared buffer (rare: it is kept at its largest size afterwards).
                        var size = buffer.Length * 2;
                        if (size > limit) size = limit;
                        var bigger = new byte[size];
                        Array.Copy(buffer, bigger, total);
                        buffer = bigger;
                    }
                    var room = buffer.Length - total;
                    if (room > limit - total) room = limit - total;
                    var read = stream.Read(buffer, total, room);
                    if (read <= 0) break;
                    var before = total;
                    total += read;
                    if (stopAt != null && Contains(buffer, before - stopAt.Length, total, stopAt)) break;
                }
            }
            return Encoding.UTF8.GetString(buffer, 0, total);
        }

        /// <summary>Whether the ASCII text occurs in buffer[from..to).</summary>
        static bool Contains(byte[] data, int from, int to, string text) {
            if (from < 0) from = 0;
            for (var i = from; i + text.Length <= to; i++) {
                var k = 0;
                while (k < text.Length && data[i + k] == text[k]) k++;
                if (k == text.Length) return true;
            }
            return false;
        }

        /// <summary>The built-in roots, each in its own array (only element 0 of an array is used).</summary>
        static X509Certificate[][] Roots() {
            if (roots == null) {
                roots = new[] {
                    new[] { new X509Certificate(Resources.GetBytes(Resources.BinaryResources.GtsRootR4)) },
                    new[] { new X509Certificate(Resources.GetBytes(Resources.BinaryResources.GlobalSignRootCA)) },
                    new[] { new X509Certificate(Resources.GetBytes(Resources.BinaryResources.DigiCertGlobalRootG2)) },
                    new[] { new X509Certificate(Resources.GetBytes(Resources.BinaryResources.GoDaddyRootG2)) },
                };
            }
            return roots;
        }
    }

    public class HttpException : Exception {
        public HttpException(int status, string body) : base("HTTP " + status) {
            this.Status = status;
            this.Body = body;
        }

        public int Status { get; }
        public string Body { get; }
    }
}
