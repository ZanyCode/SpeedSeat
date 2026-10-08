using System.Reflection;
using Microsoft.AspNetCore.SignalR;

// Keeps the microcontroller firmware in sync with the backend. Every release bundles the
// matching firmware.bin (embedded resource); after every connect the MC is asked for its
// version and, if it differs, an OTA update is triggered: the MC downloads /firmware.bin
// from this backend over HTTP, flashes it and restarts.
public class FirmwareUpdateService
{
    // Must match the Kestrel port in Program.cs — the MC downloads the firmware from it.
    public const int BackendHttpPort = 5000;

    private readonly CommandService commandService;
    private readonly IHubContext<ConnectionHub> connectionHubContext;
    private readonly IFrontendLogger frontendLogger;

    public byte[]? FirmwareBinary { get; }
    public ushort? BundledFirmwareVersion { get; }

    // Last pushed state, so a freshly (re)loaded frontend can query it.
    public string State { get; private set; } = "unknown";
    public string Message { get; private set; } = "";

    public FirmwareUpdateService(CommandService commandService, IHubContext<ConnectionHub> connectionHubContext, IFrontendLogger frontendLogger)
    {
        this.commandService = commandService;
        this.connectionHubContext = connectionHubContext;
        this.frontendLogger = frontendLogger;

        var assembly = Assembly.GetExecutingAssembly();
        using var binStream = assembly.GetManifestResourceStream("speedseat.firmware.bin");
        if (binStream != null)
        {
            using var ms = new MemoryStream();
            binStream.CopyTo(ms);
            FirmwareBinary = ms.ToArray();
        }

        using var versionStream = assembly.GetManifestResourceStream("speedseat.firmware_version.txt");
        if (versionStream != null)
        {
            using var reader = new StreamReader(versionStream);
            if (ushort.TryParse(reader.ReadToEnd().Trim(), out var version))
                BundledFirmwareVersion = version;
        }
    }

    // Guards the "version unknown" update below against looping: if the seat still doesn't
    // report a version after that update, don't flash it again on every reconnect.
    private bool updatedWithUnknownVersion = false;

    public async Task CheckFirmwareAfterConnect()
    {
        try
        {
            await PushState("checking", "Checking seat firmware version...");

            // Ask the MC for its version. Old firmwares NACK this request -> version stays null.
            var requestResult = await commandService.WriteCommand(new Command(Command.FirmwareVersionCommandId, null, null, null, false, true));
            if (requestResult == WriteResult.Success)
            {
                var deadline = DateTime.Now.AddMilliseconds(3000);
                while (commandService.ReportedFirmwareVersion == null && DateTime.Now < deadline && commandService.IsConnected)
                    await Task.Delay(50);
            }

            var reported = commandService.ReportedFirmwareVersion;

            if (BundledFirmwareVersion == null || FirmwareBinary == null)
            {
                await PushState("upToDate", $"Seat firmware version: {(reported?.ToString() ?? "unknown")}. This backend build has no bundled firmware (development build), skipping the update check.");
                return;
            }

            if (reported == BundledFirmwareVersion)
            {
                await PushState("upToDate", $"Seat firmware is up to date (version {reported}).");
                return;
            }

            if (reported == null)
            {
                // Every firmware in the field can update itself, so an unanswered version
                // request means the handshake got lost or stuck, not that the seat is too old.
                // Try the update anyway (once): a seat that really can't do it rejects the command.
                if (updatedWithUnknownVersion)
                {
                    await PushState("otaUnavailable", $"The seat still doesn't report its firmware version after an update attempt (available: {BundledFirmwareVersion}). Please use \"Flash via USB\" once — afterwards all updates happen automatically.");
                    return;
                }
                updatedWithUnknownVersion = true;
            }

            await PushState("updating", $"Updating seat firmware from version {(reported?.ToString() ?? "unknown")} to {BundledFirmwareVersion}. The seat downloads the new firmware and restarts — reconnecting automatically afterwards...");
            var updateResult = await commandService.WriteCommand(new Command(
                Command.StartFirmwareUpdateCommandId,
                new CommandValue(ValueType.Numeric, BackendHttpPort, "", scaleToFullRange: false, min: 0, max: 0xFFFF),
                null, null, false, false));

            if (updateResult != WriteResult.Success)
            {
                await PushState("otaFailed", $"The seat rejected the firmware update command ({updateResult}). Please flash firmware.bin from the release page once via USB (PlatformIO).");
                return;
            }

            // The MC is now downloading/flashing and will restart — drop the connection on our side.
            commandService.Disconnect();
        }
        catch (Exception e)
        {
            await PushState("otaFailed", $"Error during firmware update check: {e.Message}");
        }
    }

    private async Task PushState(string state, string message)
    {
        State = state;
        Message = message;
        frontendLogger.Log(message);
        await connectionHubContext.Clients.All.SendAsync("firmwareUpdateState", state, message);
    }
}
