using Microsoft.AspNetCore.SignalR;

// The backend binds to the seat on its own (see ConnectionManager): it continuously
// discovers the ESP32 over UDP and reconnects after any failure. The frontend only needs
// to read the current connection state and the firmware update progress; live changes are
// pushed via the "connectionStateChanged" and "firmwareUpdateState" events.
public class ConnectionHub : Hub
{
    private readonly CommandService commandService;
    private readonly FirmwareUpdateService firmwareUpdateService;

    public ConnectionHub(CommandService commandService, FirmwareUpdateService firmwareUpdateService)
    {
        this.commandService = commandService;
        this.firmwareUpdateService = firmwareUpdateService;
    }

    public bool GetIsConnected()
    {
        return commandService.IsConnected;
    }

    public object GetFirmwareUpdateState()
    {
        return new { state = firmwareUpdateService.State, message = firmwareUpdateService.Message };
    }
}
