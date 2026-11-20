using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Stocks;

/// <summary>
/// Joins a Wi-Fi network with the USB dongle, the same way GHI's native
/// Network.cpp does it: wpa_supplicant on the wireless interface, then udhcpc
/// for an address (its default script also writes /etc/resolv.conf).
/// Runs on a background thread; <see cref="Status"/> can be read any time.
/// The network name and password come from wifi.txt next to the program:
///   ssid=MyNetwork
///   password=secret
/// </summary>
sealed class WifiManager {
    private readonly string configPath;
    private volatile string status = "Wi-Fi not started";
    private volatile string? address;
    private string ssid = "";
    private string? iface;

    public WifiManager(string configPath) => this.configPath = configPath;

    public string Status => this.status;
    public string? Address => this.address;
    public string Ssid => this.ssid;
    public bool Connected => this.address is not null;

    public void Start() {
        var thread = new Thread(this.Run) { IsBackground = true, Name = "wifi" };
        thread.Start();
    }

    private void Run() {
        try {
            if (!this.ReadConfig(out var password)) return;
            this.Connect(password);
        }
        catch (Exception ex) {
            this.status = $"Wi-Fi error: {ex.Message}";
            Console.Error.WriteLine($"Wi-Fi: {ex}");
        }

        // Keep the status current (dongle unplugged, access point gone, ...).
        while (this.iface is not null) {
            Thread.Sleep(5000);
            this.address = this.Carrier() ? this.Ipv4() : null;
            this.status = this.address is null ? $"Not connected to {this.ssid}" : $"Connected to {this.ssid}";
        }
    }

    private bool ReadConfig(out string password) {
        password = "";
        if (!File.Exists(this.configPath)) {
            this.status = "Wi-Fi not set up (no wifi.txt)";
            return false;
        }
        foreach (var line in File.ReadAllLines(this.configPath)) {
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim().ToLowerInvariant();
            var value = line[(eq + 1)..].Trim();
            if (key == "ssid") this.ssid = value;
            if (key == "password") password = value;
        }
        if (this.ssid.Length == 0) {
            this.status = "Wi-Fi not set up (no ssid in wifi.txt)";
            return false;
        }
        return true;
    }

    private void Connect(string password) {
        this.iface = Directory.Exists("/sys/class/net")
            ? Directory.GetDirectories("/sys/class/net").Where(d => Directory.Exists(Path.Combine(d, "wireless")))
                       .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault()
            : null;
        if (this.iface is null) {
            this.status = "No Wi-Fi adapter found";
            return;
        }

        // Already on the right network with an address (for example after a restart of the program)?
        var state = Run("/usr/sbin/wpa_cli", "-i", this.iface, "status");
        if (state.Contains("wpa_state=COMPLETED") && state.Contains($"\nssid={this.ssid}\n") && this.Ipv4() is { } existing) {
            this.address = existing;
            this.status = $"Connected to {this.ssid}";
            Console.WriteLine($"Wi-Fi: already connected to {this.ssid} as {existing}");
            return;
        }

        this.status = $"Connecting to {this.ssid}…";
        Console.WriteLine($"Wi-Fi: joining {this.ssid} on {this.iface}");
        var conf = $"/run/wpa_supplicant.{this.iface}.conf";
        var hexSsid = Convert.ToHexString(Encoding.UTF8.GetBytes(this.ssid)).ToLowerInvariant();
        var security = password.Length == 0 ? "  key_mgmt=NONE\n" : $"  psk=\"{password}\"\n  key_mgmt=WPA-PSK\n";
        File.WriteAllText(conf, $"ctrl_interface=/var/run/wpa_supplicant\nnetwork={{\n  ssid={hexSsid}\n{security}}}\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(conf, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Run("/usr/sbin/wpa_cli", "-i", this.iface, "terminate");
        Run("/sbin/ip", "link", "set", this.iface, "up");
        Run("/usr/sbin/wpa_supplicant", "-B", "-i", this.iface, "-c", conf, "-P", $"/var/run/wpa_supplicant.{this.iface}.pid");

        if (!WaitFor(this.Carrier, 20_000)) {
            this.status = $"Could not join {this.ssid} (check the password in wifi.txt)";
            return;
        }

        this.status = $"Getting an address from {this.ssid}…";
        var pidFile = $"/var/run/udhcpc.{this.iface}.pid";
        if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid)) {
            try { Process.GetProcessById(pid).Kill(); } catch { }
        }
        Run("/sbin/udhcpc", "-i", this.iface, "-p", pidFile, "-b", "-t", "10", "-T", "2");

        string? ip = null;
        WaitFor(() => (ip = this.Ipv4()) is not null, 25_000);
        this.address = ip;
        this.status = ip is null ? $"Joined {this.ssid} but got no address" : $"Connected to {this.ssid}";
        Console.WriteLine($"Wi-Fi: {this.status} {ip}");
    }

    private bool Carrier() {
        try { return File.ReadAllText($"/sys/class/net/{this.iface}/carrier").Trim() == "1"; }
        catch { return false; }
    }

    private string? Ipv4() {
        var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Name == this.iface);
        return nic?.GetIPProperties().UnicastAddresses
                  .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs) {
        var end = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < end) {
            if (condition()) return true;
            Thread.Sleep(250);
        }
        return condition();
    }

    /// <summary>Run a program and return what it printed (empty if it could not start).</summary>
    private static string Run(string file, params string[] args) {
        try {
            var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in args) info.ArgumentList.Add(a);
            using var p = Process.Start(info);
            if (p is null) return "";
            var output = p.StandardOutput.ReadToEndAsync();
            var error = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(15_000)) {
                try { p.Kill(); } catch { }
            }
            // A daemon (wpa_supplicant -B, udhcpc -b) could keep the pipes open: don't wait for them.
            Task.WaitAll(new Task[] { output, error }, 2000);
            return (output.IsCompletedSuccessfully ? output.Result : "") + (error.IsCompletedSuccessfully ? error.Result : "");
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Wi-Fi: {file} failed: {ex.Message}");
            return "";
        }
    }
}
