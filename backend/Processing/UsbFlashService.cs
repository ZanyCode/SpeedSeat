using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using Microsoft.AspNetCore.SignalR;

// Flashes the bundled ESP32 firmware to a seat connected over USB — used for first-time
// setup or recovery, when the seat has no (compatible) firmware yet and therefore can't be
// reached over WiFi. Everything needed is embedded in the exe: the standalone esptool plus
// the full image set (bootloader, partition table, boot_app0, app). At runtime they're
// written to a temp directory and esptool flashes them at their standard offsets.
//
// Note: this drives the USB serial port directly via esptool; it is independent of the
// WiFi/UDP protocol connection (CommandService/ConnectionManager never touch COM ports).
public class UsbFlashService
{
    private readonly IFrontendLogger logger;
    private readonly IHubContext<ConnectionHub> hub;

    private readonly byte[]? esptool;
    private readonly byte[]? bootloader;
    private readonly byte[]? partitions;
    private readonly byte[]? bootApp0;
    private readonly byte[]? firmware;

    // True only for packaged builds that actually embed the flash payload (CI publish).
    public bool IsAvailable => esptool != null && bootloader != null && partitions != null && bootApp0 != null && firmware != null;

    public UsbFlashService(IFrontendLogger logger, IHubContext<ConnectionHub> hub)
    {
        this.logger = logger;
        this.hub = hub;

        var asm = Assembly.GetExecutingAssembly();
        esptool = ReadResource(asm, "speedseat.esptool.exe");
        bootloader = ReadResource(asm, "speedseat.bootloader.bin");
        partitions = ReadResource(asm, "speedseat.partitions.bin");
        bootApp0 = ReadResource(asm, "speedseat.boot_app0.bin");
        firmware = ReadResource(asm, "speedseat.firmware.bin");
    }

    public async Task<bool> FlashAsync()
    {
        if (!IsAvailable)
        {
            await PushState("failed", "USB flashing isn't available in this build (no bundled esptool/firmware). Flash once via PlatformIO instead.");
            return false;
        }

        var ports = SerialPort.GetPortNames();
        if (ports.Length == 0)
        {
            await PushState("failed", "No USB serial port found. Connect the seat to this PC with a USB cable and try again.");
            return false;
        }

        var workDir = Path.Combine(Path.GetTempPath(), "speedseat-usbflash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var esptoolPath = Path.Combine(workDir, "esptool.exe");
            var bootloaderPath = Path.Combine(workDir, "bootloader.bin");
            var partitionsPath = Path.Combine(workDir, "partitions.bin");
            var bootApp0Path = Path.Combine(workDir, "boot_app0.bin");
            var firmwarePath = Path.Combine(workDir, "firmware.bin");
            await File.WriteAllBytesAsync(esptoolPath, esptool!);
            await File.WriteAllBytesAsync(bootloaderPath, bootloader!);
            await File.WriteAllBytesAsync(partitionsPath, partitions!);
            await File.WriteAllBytesAsync(bootApp0Path, bootApp0!);
            await File.WriteAllBytesAsync(firmwarePath, firmware!);

            // The seat is the only ESP32, but the PC may expose several COM ports (Bluetooth
            // etc.). Try each until one flashes; esptool fails fast on ports with no ESP.
            foreach (var port in ports)
            {
                await PushState("flashing", $"Flashing the seat on {port}… this takes about 20 seconds. Do not unplug it.");

                var args = $"--chip esp32 --port {port} --baud 460800 --before default_reset --after hard_reset " +
                           "write_flash -z --flash_mode dio --flash_freq 40m --flash_size detect " +
                           $"0x1000 \"{bootloaderPath}\" 0x8000 \"{partitionsPath}\" 0xe000 \"{bootApp0Path}\" 0x10000 \"{firmwarePath}\"";

                var (success, output) = await RunEsptool(esptoolPath, args, workDir);
                logger.Log($"USB flash attempt on {port} {(success ? "succeeded" : "failed")}.\n{output}");

                if (success)
                {
                    await PushState("success", $"Seat flashed successfully on {port}. It is restarting and will open the 'SpeedSeat-Setup' WiFi access point for first-time WiFi setup.");
                    return true;
                }
            }

            await PushState("failed", "Flashing failed on every detected USB port. Make sure the seat is connected and no other program (Arduino IDE, serial monitor) is using the port.");
            return false;
        }
        catch (Exception e)
        {
            logger.Log($"USB flashing error: {e.Message}");
            await PushState("failed", $"USB flashing error: {e.Message}");
            return false;
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* temp dir cleanup is best-effort */ }
        }
    }

    private async Task<(bool success, string output)> RunEsptool(string exePath, string args, string workDir)
    {
        var psi = new ProcessStartInfo(exePath, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workDir
        };

        using var proc = new Process { StartInfo = psi };
        var output = new System.Text.StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();

        return (proc.ExitCode == 0, output.ToString());
    }

    private static byte[]? ReadResource(Assembly asm, string name)
    {
        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null)
            return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private async Task PushState(string state, string message)
    {
        logger.Log(message);
        await hub.Clients.All.SendAsync("usbFlashState", state, message);
    }
}
