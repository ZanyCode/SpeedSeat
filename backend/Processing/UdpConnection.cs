using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

// Thin abstraction over the byte-stream link to the microcontroller. The only transport
// is WiFi/UDP — the backend never talks to the seat over USB-serial.
public interface IDeviceConnection : IDisposable
{
    event EventHandler? DataReceived;

    void Open();

    void Write(byte[] buffer, int offset, int count);

    int Read(byte[] data, int offset, int count);

    void FakeWriteBytes(byte[] bytes);

    public int BytesToRead { get; }
}

public interface IDeviceConnectionFactory
{
    public IDeviceConnection Create(string address);
}

public class DeviceConnectionFactory : IDeviceConnectionFactory
{
    private readonly IFrontendLogger frontendLogger;

    public DeviceConnectionFactory(IFrontendLogger frontendLogger)
    {
        this.frontendLogger = frontendLogger;
    }

    public IDeviceConnection Create(string address)
    {
        // ESP32 controllers are discovered and addressed by their IP — UDP is the only transport.
        var ipAddress = IPAddress.Parse(address);
        return new UdpDeviceConnection(new IPEndPoint(ipAddress, SpeedseatUdpProtocol.Port), frontendLogger);
    }
}

// Shared constants of the UDP transport. The ESP32 firmware (microcontroller/include/configuration.h
// and src/transport.cpp) must use the same port and magic strings.
public static class SpeedseatUdpProtocol
{
    public const int Port = 8888;
    public const string DiscoveryRequest = "SPEEDSEAT_DISCOVERY";
    public const string DiscoveryResponse = "SPEEDSEAT_ESP32";
}

public static class UdpSocketHelper
{
    // Windows quirk: when a sent datagram triggers an ICMP "port unreachable" (device
    // rebooting, WiFi hiccup), the socket gets poisoned and every following Send/Receive
    // throws SocketException 10054 (ConnectionReset). Disable that behavior.
    public static void DisableIcmpConnectionReset(UdpClient udpClient)
    {
        if (!OperatingSystem.IsWindows())
            return;

        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
        udpClient.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
    }
}

// Finds SpeedSeat ESP32 controllers on the local network by broadcasting a discovery
// packet and collecting the IPs of all devices that answer with the discovery response.
public static class EspDiscovery
{
    public static async Task<string[]> Discover(int timeoutMs, IFrontendLogger frontendLogger)
    {
        var foundIps = new HashSet<string>();
        try
        {
            using var udp = new UdpClient();
            UdpSocketHelper.DisableIcmpConnectionReset(udp);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            var request = Encoding.ASCII.GetBytes(SpeedseatUdpProtocol.DiscoveryRequest);

            // 255.255.255.255 only goes out on one interface on multi-NIC machines,
            // so additionally send a directed broadcast on every IPv4 interface.
            foreach (var broadcastAddress in GetBroadcastAddresses())
            {
                try
                {
                    await udp.SendAsync(request, request.Length, new IPEndPoint(broadcastAddress, SpeedseatUdpProtocol.Port));
                }
                catch (Exception e)
                {
                    frontendLogger.Log($"ESP-Discovery: Could not send broadcast to {broadcastAddress}: {e.Message}");
                }
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(cts.Token);
                    var payload = Encoding.ASCII.GetString(result.Buffer);
                    if (payload == SpeedseatUdpProtocol.DiscoveryResponse)
                    {
                        if (foundIps.Add(result.RemoteEndPoint.Address.ToString()))
                            frontendLogger.Log($"ESP-Discovery: Found SpeedSeat controller at {result.RemoteEndPoint.Address}");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            frontendLogger.Log($"ESP-Discovery: Error during discovery: {e.Message}");
        }

        return foundIps.ToArray();
    }

    private static IEnumerable<IPAddress> GetBroadcastAddresses()
    {
        yield return IPAddress.Broadcast;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null)
                    continue;

                var addressBytes = unicast.Address.GetAddressBytes();
                var maskBytes = unicast.IPv4Mask.GetAddressBytes();
                var broadcastBytes = new byte[4];
                for (int i = 0; i < 4; i++)
                    broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);

                yield return new IPAddress(broadcastBytes);
            }
        }
    }
}

// Connection to the ESP32 over UDP. Implements IDeviceConnection so CommandService
// (8-byte protocol, ack handling, 0x01/0x02 connection handshake) works unchanged.
// Every Write sends one datagram; received datagrams are buffered and exposed
// through BytesToRead/Read like a byte stream.
public class UdpDeviceConnection : IDeviceConnection
{
    private readonly IPEndPoint endpoint;
    private readonly IFrontendLogger frontendLogger;
    private UdpClient? udpClient;
    private CancellationTokenSource? receiveCts;
    private readonly ConcurrentQueue<byte> receivedBytes = new ConcurrentQueue<byte>();

    private bool isSimulating = false;
    private byte[]? simulatedData = null;

    public event EventHandler? DataReceived;

    public int BytesToRead => isSimulating ? simulatedData!.Length : receivedBytes.Count;

    public UdpDeviceConnection(IPEndPoint endpoint, IFrontendLogger frontendLogger)
    {
        this.endpoint = endpoint;
        this.frontendLogger = frontendLogger;
    }

    public void Open()
    {
        udpClient = new UdpClient();
        UdpSocketHelper.DisableIcmpConnectionReset(udpClient);
        udpClient.Connect(endpoint);
        receiveCts = new CancellationTokenSource();
        var token = receiveCts.Token;
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(token);
                    foreach (var b in result.Buffer)
                        receivedBytes.Enqueue(b);

                    DataReceived?.Invoke(this, EventArgs.Empty);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    frontendLogger.Log($"UDP-Connection: Error receiving data from {endpoint}: {e.Message}");
                    await Task.Delay(100, CancellationToken.None); // avoid hot-looping if the socket keeps erroring
                }
            }
        });
        frontendLogger.Log($"Successfully opened UDP connection to {endpoint}");
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        if (isSimulating) // First write after simulating mc command is the response byte, we don't actually want to send this
        {
            isSimulating = false;
            return;
        }

        if (udpClient == null)
            throw new Exception("Attempted to write to a closed UDP connection");

        try
        {
            udpClient.Send(buffer.Skip(offset).Take(count).ToArray(), count);
        }
        catch (SocketException e)
        {
            // Treat a failed send like a dropped datagram: CommandService's ack timeout and
            // 3-attempt retry recover from that, while an exception would abort immediately.
            frontendLogger.Log($"UDP-Connection: Sending datagram to {endpoint} failed ({e.SocketErrorCode}). Treating it as dropped, the retry mechanism will resend.");
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (isSimulating)
        {
            Array.Copy(simulatedData!.Skip(offset).ToArray(), buffer, count);
            return count;
        }

        int bytesRead = 0;
        while (bytesRead < count && receivedBytes.TryDequeue(out var b))
        {
            buffer[offset + bytesRead] = b;
            bytesRead++;
        }
        return bytesRead;
    }

    public void FakeWriteBytes(byte[] bytes)
    {
        isSimulating = true;
        simulatedData = bytes;
        DataReceived?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        receiveCts?.Cancel();
        udpClient?.Dispose();
        udpClient = null;
    }
}
