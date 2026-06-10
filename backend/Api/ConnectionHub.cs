using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.IO.Ports;

public class ConnectionHub : Hub
{
    private readonly Speedseat seat;
    private readonly ISpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly IHubContext<ConnectionHub> context;
    private readonly IOptionsMonitor<Config> options;
    private readonly IFrontendLogger frontendLogger;

    public ConnectionHub(Speedseat seat, ISpeedseatSettings settings, CommandService commandService, IHubContext<ConnectionHub> context, IOptionsMonitor<Config> options, IFrontendLogger frontendLogger)
    {
        this.seat = seat;
        this.settings = settings;
        this.commandService = commandService;
        this.context = context;
        this.options = options;
        this.frontendLogger = frontendLogger;
    }

    public async Task<string[]> GetPorts()
    {
        // ESP32 controllers discovered via UDP broadcast are listed by IP address
        // next to the classic COM ports; the factory picks the transport from the format.
        var espIps = await EspDiscovery.Discover(options.CurrentValue.EspDiscoveryTimeoutMs, frontendLogger);
        return espIps.Concat(SerialPort.GetPortNames()).ToArray();
    }

    public bool GetIsConnected()
    {
        return commandService.IsConnected;
    }

    public async Task<bool> Connect(string port)
    {
        return await commandService.Connect(port);
    }

    public async Task DeleteEEPROM(string port)
    {
        this.commandService.DeleteEEPROM(port);
    }

    public void CancelConnectionProcess()
    {
        commandService.CancelConnectionProcess();
    }

    public void Disconnect()
    {
        commandService.Disconnect();
    }

    public void FakeConnectionConfirmation()
    {
        this.commandService.FakeWriteRequest(new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false));
    }
}
