using Microsoft.AspNetCore.SignalR;

// The backend binds to the seat on its own (see ConnectionManager): it continuously
// discovers the ESP32 over UDP and reconnects after any failure. The frontend only needs
// to read the current connection state and the firmware update progress; live changes are
// pushed via the "connectionStateChanged" and "firmwareUpdateState" events.
public class ConnectionHub : Hub
{
    private readonly CommandService commandService;
    private readonly FirmwareUpdateService firmwareUpdateService;
    private readonly UsbFlashService usbFlashService;

    public ConnectionHub(CommandService commandService, FirmwareUpdateService firmwareUpdateService, UsbFlashService usbFlashService)
    {
        this.commandService = commandService;
        this.firmwareUpdateService = firmwareUpdateService;
        this.usbFlashService = usbFlashService;
    }

    public bool GetIsConnected()
    {
        return commandService.IsConnected;
    }

    public object GetFirmwareUpdateState()
    {
        return new { state = firmwareUpdateService.State, message = firmwareUpdateService.Message };
    }

    // Whether this build can flash a USB-connected seat (only packaged release builds bundle
    // the esptool + firmware images). The frontend hides the "Flash via USB" button otherwise.
    public bool GetCanFlashViaUsb()
    {
        return usbFlashService.IsAvailable;
    }

    // Flashes the bundled firmware to a USB-connected seat (first-time setup / recovery).
    // Runs in the background; progress is pushed to all clients via the "usbFlashState" event.
    public void FlashViaUsb()
    {
        _ = Task.Run(() => usbFlashService.FlashAsync());
    }
}
