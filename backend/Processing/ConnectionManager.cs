using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

// Keeps the backend bound to the seat no matter what. While disconnected it discovers
// ESP32 controllers over UDP broadcast every couple of seconds and connects to the first
// one that answers; after a successful connect it runs the firmware version check / OTA
// update. Any transmission failure makes CommandService drop the connection, which this
// loop then re-establishes — so a seat reboot, WiFi hiccup or OTA restart all recover
// automatically without any user interaction.
public class ConnectionManager : BackgroundService
{
    private const int ReconnectIntervalMs = 2000;

    private readonly CommandService commandService;
    private readonly FirmwareUpdateService firmwareUpdateService;
    private readonly IFrontendLogger logger;
    private readonly IOptionsMonitor<Config> options;
    private readonly IHubContext<ConnectionHub> hub;

    public ConnectionManager(CommandService commandService, FirmwareUpdateService firmwareUpdateService, IFrontendLogger logger, IOptionsMonitor<Config> options, IHubContext<ConnectionHub> hub)
    {
        this.commandService = commandService;
        this.firmwareUpdateService = firmwareUpdateService;
        this.logger = logger;
        this.options = options;
        this.hub = hub;

        // Forward every connect/disconnect to all connected frontends so the UI overlay
        // reflects the live state instead of guessing.
        this.commandService.ConnectionStateChanged += isConnected =>
            _ = hub.Clients.All.SendAsync("connectionStateChanged", isConnected);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!commandService.IsConnected)
                    await TryConnect();
            }
            catch (Exception e)
            {
                logger.Log($"Auto-connect: unexpected error: {e.Message}");
            }

            await Task.Delay(ReconnectIntervalMs, stoppingToken);
        }
    }

    private async Task TryConnect()
    {
        var espIps = await EspDiscovery.Discover(options.CurrentValue.EspDiscoveryTimeoutMs, logger);
        foreach (var ip in espIps)
        {
            if (commandService.IsConnected)
                return;

            if (await commandService.Connect(ip))
            {
                // Version handshake + OTA run in the background so this loop keeps ticking;
                // progress is pushed to the frontend via the "firmwareUpdateState" event.
                _ = Task.Run(() => firmwareUpdateService.CheckFirmwareAfterConnect());
                return;
            }
        }
    }
}
