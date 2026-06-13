using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

// Installs a newer backend release in place: downloads the release's speedseat.exe next to
// the running one, swaps it in and relaunches — so the seat keeps its database/config.json
// (both live next to the exe) across the update instead of the user downloading a loose copy.
//
// Windows won't let a running .exe be overwritten or deleted, but it *can* be renamed/moved.
// So the running exe is renamed to <exe>.old, the freshly downloaded file takes its place,
// the new exe is started and this process exits (releasing the lock). The leftover <exe>.old
// is deleted on the next startup (see CleanupOldVersion).
//
// If anything makes the in-place swap impossible (dev build, missing permissions, a failed
// download) the method reports failure and the frontend falls back to opening the download
// URL in the browser.
public class SelfUpdateService
{
    // Passed to the relaunched exe so it doesn't open a second browser window — the existing
    // frontend tab reloads itself onto the new backend instead.
    public const string UpdatedRelaunchArg = "--updated";

    private const string PackagedExeName = "speedseat.exe";

    private readonly UpdateCheckService updateCheckService;
    private readonly IFrontendLogger logger;
    private readonly IHubContext<InfoHub> hub;

    public SelfUpdateService(UpdateCheckService updateCheckService, IFrontendLogger logger, IHubContext<InfoHub> hub)
    {
        this.updateCheckService = updateCheckService;
        this.logger = logger;
        this.hub = hub;
    }

    // In-place update only makes sense for the packaged single-file exe. Under `dotnet run`
    // the process is dotnet.exe / speedseat.dll, which we must never rename.
    public bool IsInPlaceUpdateSupported()
    {
        var path = Environment.ProcessPath;
        return path != null && Path.GetFileName(path).Equals(PackagedExeName, StringComparison.OrdinalIgnoreCase);
    }

    // Best-effort removal of the previous version left behind by the last successful update.
    public void CleanupOldVersion()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (path == null)
                return;

            var oldPath = path + ".old";
            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
                logger.Log("Self-update: cleaned up the previous version after a successful update.");
            }
        }
        catch (Exception e)
        {
            logger.Log($"Self-update: could not remove the previous version (will retry next start): {e.Message}");
        }
    }

    // Returns true when the in-place update has started — the app will relaunch and exit
    // momentarily. Returns false when it isn't possible; the caller should then fall back to
    // a manual browser download.
    public async Task<bool> InstallUpdate()
    {
        if (!IsInPlaceUpdateSupported())
        {
            logger.Log("Self-update: in-place update is not supported for this build (not the packaged speedseat.exe). Falling back to manual download.");
            return false;
        }

        var info = await updateCheckService.GetUpdateInfo();
        if (!info.UpdateAvailable || info.DownloadUrl == null)
        {
            await PushState("failed", "No update is available to install.");
            return false;
        }

        var exePath = Environment.ProcessPath!;
        var dir = Path.GetDirectoryName(exePath)!;
        var tmpPath = Path.Combine(dir, "speedseat.update.tmp");
        var oldPath = exePath + ".old";

        try
        {
            await PushState("downloading", $"Downloading update {info.LatestVersion}...");
            await DownloadTo(info.DownloadUrl, tmpPath);
        }
        catch (Exception e)
        {
            logger.Log($"Self-update: download failed: {e.Message}");
            TryDelete(tmpPath);
            await PushState("failed", "Downloading the update failed.");
            return false;
        }

        try
        {
            // Rename the running exe out of the way, then move the new one into its place.
            TryDelete(oldPath);
            File.Move(exePath, oldPath);
            try
            {
                File.Move(tmpPath, exePath);
            }
            catch
            {
                // Couldn't put the new file in place — restore the original and abort.
                File.Move(oldPath, exePath);
                throw;
            }
        }
        catch (Exception e)
        {
            logger.Log($"Self-update: could not replace the executable ({e.Message}). Falling back to manual download.");
            TryDelete(tmpPath);
            await PushState("failed", "Could not replace the program file (permissions?). Falling back to manual download.");
            return false;
        }

        await PushState("restarting", "Update installed. Restarting SpeedSeat...");

        // Relaunch + exit *after* this call returns, so the caller still receives `true` and
        // the file lock is released only once we're shutting down.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            try
            {
                Process.Start(new ProcessStartInfo(exePath, UpdatedRelaunchArg) { UseShellExecute = true, WorkingDirectory = dir });
            }
            catch (Exception e)
            {
                logger.Log($"Self-update: failed to relaunch the new version: {e.Message}");
            }
            Environment.Exit(0);
        });

        return true;
    }

    private async Task DownloadTo(string url, string destination)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(5);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SpeedSeat-SelfUpdate");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using (var src = await response.Content.ReadAsStreamAsync())
        await using (var dst = File.Create(destination))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            int lastPercent = -1;
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total is > 0)
                {
                    int percent = (int)(readTotal * 100 / total.Value);
                    if (percent != lastPercent && percent % 10 == 0)
                    {
                        lastPercent = percent;
                        await PushState("downloading", $"Downloading update... {percent}%");
                    }
                }
            }
        }

        var downloadedSize = new FileInfo(destination).Length;
        if (total.HasValue && downloadedSize != total.Value)
            throw new Exception($"Downloaded size ({downloadedSize}) does not match the expected size ({total.Value}).");
        if (downloadedSize < 1_000_000)
            throw new Exception($"Downloaded file is implausibly small ({downloadedSize} bytes).");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }

    private async Task PushState(string state, string message)
    {
        logger.Log(message);
        await hub.Clients.All.SendAsync("updateInstallState", state, message);
    }
}
