using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

// Opt-in link debugger for hunting connection hiccups. Enabled by setting the environment
// variable SPEEDSEAT_DIAG_DIR to a directory; without it every call is a no-op.
//
// Writes one timestamped line per event to <dir>/diag-<start time>.log:
//   TX / ACK / TIMEOUT   every command datagram sent to the seat, its ack round-trip time
//   RX / ACKTX           every datagram received from the seat and the ack we answer with
//   CONN / LOG           connect, disconnect (with reason) and all frontend-logger messages
//   TELE                 per-second game telemetry rate and the largest gap between packets
//   PING                 ICMP round-trip to the seat and to the WiFi gateway (tells a seat-side
//                        problem from a PC/router-side one)
//   WIFI                 signal / rate / channel / BSSID of the PC's WiFi adapter
//   STALL                the process itself froze (GC, thread-pool starvation)
public static class ConnectionDiagnostics
{
    private static readonly string? directory = Environment.GetEnvironmentVariable("SPEEDSEAT_DIAG_DIR");
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static readonly BlockingCollection<string> lines = new BlockingCollection<string>();

    private static volatile string? seatIp;
    private static long telemetryCount;
    private static long lastTelemetryTicks;
    private static long maxTelemetryGapTicks;

    public static bool Enabled => directory != null;

    // Milliseconds since process start, for measuring round-trip times at the call site.
    public static double NowMs => clock.Elapsed.TotalMilliseconds;

    public static void Start()
    {
        if (!Enabled)
            return;

        Directory.CreateDirectory(directory!);
        var path = Path.Combine(directory!, $"diag-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        new Thread(() => WriteLoop(path)) { IsBackground = true, Name = "diag-writer" }.Start();
        new Thread(StallLoop) { IsBackground = true, Name = "diag-stall" }.Start();
        new Thread(() => PingLoop(() => seatIp, "seat")) { IsBackground = true, Name = "diag-ping-seat" }.Start();
        new Thread(() => PingLoop(GetGatewayIp, "gateway")) { IsBackground = true, Name = "diag-ping-gw" }.Start();
        new Thread(WifiLoop) { IsBackground = true, Name = "diag-wifi" }.Start();
        new Thread(TelemetryLoop) { IsBackground = true, Name = "diag-tele" }.Start();
        Event("START", $"pid={Environment.ProcessId} file={path}");
    }

    public static void Event(string kind, string message)
    {
        if (!Enabled)
            return;

        lines.Add($"{DateTime.Now:HH:mm:ss.fff} {clock.Elapsed.TotalMilliseconds,11:F1} {kind,-8} {message}");
    }

    public static void SetSeatIp(string? ip) => seatIp = ip;

    // Called for every motion packet the game delivers; only counters on the hot path.
    public static void TelemetryPacket()
    {
        if (!Enabled)
            return;

        long now = clock.ElapsedTicks;
        long last = Interlocked.Exchange(ref lastTelemetryTicks, now);
        if (last != 0)
        {
            long gap = now - last;
            long currentMax;
            while (gap > (currentMax = Interlocked.Read(ref maxTelemetryGapTicks)) &&
                   Interlocked.CompareExchange(ref maxTelemetryGapTicks, gap, currentMax) != currentMax) { }
        }
        Interlocked.Increment(ref telemetryCount);
    }

    private static void WriteLoop(string path)
    {
        using var writer = new StreamWriter(path, append: true) { AutoFlush = false };
        foreach (var line in lines.GetConsumingEnumerable())
        {
            writer.WriteLine(line);
            if (lines.Count == 0)
                writer.Flush();
        }
    }

    // A dedicated thread that should wake every 20 ms. If it wakes much later the whole
    // process was frozen (GC pause, CPU starvation by the game) — not a network problem.
    private static void StallLoop()
    {
        double last = NowMs;
        while (true)
        {
            Thread.Sleep(20);
            double now = NowMs;
            if (now - last > 100)
                Event("STALL", $"process paused for {now - last:F0}ms");
            last = now;
        }
    }

    private static void TelemetryLoop()
    {
        while (true)
        {
            Thread.Sleep(1000);
            long count = Interlocked.Exchange(ref telemetryCount, 0);
            long maxGap = Interlocked.Exchange(ref maxTelemetryGapTicks, 0);
            if (count > 0)
                Event("TELE", $"packets={count} maxGapMs={maxGap * 1000.0 / Stopwatch.Frequency:F1}");
        }
    }

    private static void PingLoop(Func<string?> target, string name)
    {
        using var ping = new Ping();
        while (true)
        {
            Thread.Sleep(250);
            var ip = target();
            if (ip == null)
                continue;

            try
            {
                var reply = ping.Send(ip, 1000);
                if (reply.Status != IPStatus.Success)
                    Event("PING", $"{name} {ip} FAILED {reply.Status}");
                else
                    Event("PING", $"{name} {ip} {reply.RoundtripTime}ms");
            }
            catch (Exception e)
            {
                Event("PING", $"{name} {ip} ERROR {e.InnerException?.Message ?? e.Message}");
            }
        }
    }

    // The access point the PC is associated with. Falls back to the ARP/route-less case
    // (e.g. a phone hotspot that only announces an IPv6 gateway) via SPEEDSEAT_DIAG_GATEWAY.
    private static string? GetGatewayIp()
    {
        var configured = Environment.GetEnvironmentVariable("SPEEDSEAT_DIAG_GATEWAY");
        if (!string.IsNullOrEmpty(configured))
            return configured;

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .SelectMany(nic => nic.GetIPProperties().GatewayAddresses)
                .Select(gateway => gateway.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any))
                ?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void WifiLoop()
    {
        // Matches both the English and the localized (German) netsh labels.
        var interesting = new Regex(@"^\s*(Signal|BSSID|Kanal|Channel|Status|State|Empfangsrate|Receive rate|.bertragungsrate|Transmit rate)[^:]*:\s*(.+?)\s*$", RegexOptions.IgnoreCase);
        string? previous = null;
        while (true)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var process = Process.Start(psi)!;
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                var summary = string.Join(" | ", output.Split('\n')
                    .Select(line => interesting.Match(line))
                    .Where(match => match.Success)
                    .Select(match => $"{match.Groups[1].Value.Trim()}={match.Groups[2].Value}"));
                if (summary != previous)
                    Event("WIFI", summary.Length == 0 ? "no wifi interface" : summary);
                previous = summary;
            }
            catch (Exception e)
            {
                Event("WIFI", $"ERROR {e.Message}");
            }
            Thread.Sleep(2000);
        }
    }
}
