using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "";
    public string? LatestVersion { get; set; }
    public bool UpdateAvailable { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Error { get; set; }
}

// Checks the public GitHub releases of this repository for a newer backend version.
// Queried once at startup (result is cached); the frontend shows a download button
// when a newer release exists.
public class UpdateCheckService
{
    private const string GithubRepo = "ZanyCode/SpeedSeat";

    private readonly IFrontendLogger frontendLogger;
    private Task<UpdateInfo>? cachedCheck;

    public UpdateCheckService(IFrontendLogger frontendLogger)
    {
        this.frontendLogger = frontendLogger;
    }

    public Task<UpdateInfo> GetUpdateInfo()
    {
        // Lazy + cached: the GitHub API rate limit is 60 requests/h for anonymous clients.
        cachedCheck ??= CheckForUpdate();
        return cachedCheck;
    }

    private async Task<UpdateInfo> CheckForUpdate()
    {
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 1);
        var info = new UpdateInfo { CurrentVersion = currentVersion.ToString() };

        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SpeedSeat-UpdateCheck");
            var json = await http.GetStringAsync($"https://api.github.com/repos/{GithubRepo}/releases/latest");
            var release = JsonSerializer.Deserialize<GithubRelease>(json);

            if (release?.TagName == null || !Version.TryParse(release.TagName.TrimStart('v'), out var latestVersion))
            {
                info.Error = $"Could not parse latest release tag '{release?.TagName}'";
                return info;
            }

            info.LatestVersion = latestVersion.ToString();
            info.UpdateAvailable = latestVersion > currentVersion;
            info.DownloadUrl = release.Assets?.FirstOrDefault(a => a.Name != null && a.Name.EndsWith(".exe"))?.BrowserDownloadUrl
                               ?? release.HtmlUrl;

            frontendLogger.Log(info.UpdateAvailable
                ? $"Update check: new version {info.LatestVersion} is available (installed: {info.CurrentVersion})."
                : $"Update check: version {info.CurrentVersion} is up to date (latest release: {info.LatestVersion}).");
        }
        catch (Exception e)
        {
            info.Error = e.Message;
            frontendLogger.Log($"Update check failed (no internet connection?): {e.Message}");
        }

        return info;
    }

    private class GithubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GithubAsset>? Assets { get; set; }
    }

    private class GithubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
