using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

// Single-instance guard: only one backend can own port 5000. A second launch must not crash
// with "address already in use" — instead bring the running instance's UI to the front and
// exit. We key off the port itself (not a mutex) so this also covers an older build or any
// other process already holding 5000.
bool isUpdateRelaunch = args.Contains(SelfUpdateService.UpdatedRelaunchArg);

if (isUpdateRelaunch)
{
    // A self-update relaunch races the outgoing instance for port 5000 — wait for it to free.
    for (int i = 0; i < 50 && IsPortInUse(5000); i++)
        Thread.Sleep(200);
}
else if (IsPortInUse(5000))
{
    // Another instance already serves the UI — surface it and exit instead of crashing.
    SurfaceRunningInstance();
    return;
}

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonStream(GetConfigJSONStream());
    builder.Configuration.AddJsonStream(GetAppsettingsJSONStream());
    builder.Services.Configure<Config>(builder.Configuration.GetSection("Config"));
    builder.Services.AddSignalR();
    builder.Services.AddCors();
    builder.Services.AddSingleton<ISpeedseatSettings, SpeedseatSettings>();
    builder.Services.AddSingleton<Speedseat>();
    builder.Services.AddSingleton<CommandService>();
    builder.Services.AddSingleton<OutdatedDataDiscardQueue<Command>>();
    builder.Services.AddSingleton<Speedseat>();
    builder.Services.AddSingleton<F12020TelemetryAdaptor>();
    builder.Services.AddSingleton<FirmwareUpdateService>();
    builder.Services.AddSingleton<UpdateCheckService>();
    builder.Services.AddSingleton<SelfUpdateService>();
    builder.Services.AddSingleton<UsbFlashService>();
    builder.Services.AddSingleton<IFrontendLogger, FrontendLogger>();
    builder.Services.AddTransient<IDeviceConnectionFactory, DeviceConnectionFactory>();
    // Continuously discovers the ESP32 over UDP and keeps the backend bound to the seat.
    builder.Services.AddHostedService<ConnectionManager>();

    builder.Services.AddDbContext<SpeedseatContext>(options => options.UseSqlite("Data Source=speedseat_dbversion2.sqlite3"));
    // builder.Services.AddHostedService<F12020TelemetryAdaptor>();
    builder.Services.AddHttpContextAccessor();
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5000); // to listen for incoming http connection on port 5001
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    }

    app.UseCors(o =>
    {
        o.AllowAnyHeader();
        o.AllowAnyMethod();
        o.AllowCredentials();
        o.SetIsOriginAllowed(origin => true);
    });

    app.UseSpaStaticFiles(new StaticFileOptions
    {
        FileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot")
    });
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot")
    });
    app.UseSpa(spa =>
    {
        spa.Options.DefaultPageStaticFileOptions = new StaticFileOptions
        {
            FileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot")
        };
    });

    // Telemetry processing runs for the whole lifetime of the backend — the game is
    // auto-detected from the incoming packets, there is no streaming state to manage.
    app.Services.GetRequiredService<F12020TelemetryAdaptor>().Start();

    // The ESP32 downloads the bundled firmware from here during an OTA update.
    var firmwareService = app.Services.GetRequiredService<FirmwareUpdateService>();
    app.MapGet("/firmware.bin", () => firmwareService.FirmwareBinary != null
        ? Results.Bytes(firmwareService.FirmwareBinary, "application/octet-stream", "firmware.bin")
        : Results.NotFound());

    // Remove the previous executable left behind by the last in-place self-update (if any).
    app.Services.GetRequiredService<SelfUpdateService>().CleanupOldVersion();

    // Check for new releases in the background; the frontend picks the result up via InfoHub.
    _ = Task.Run(() => app.Services.GetRequiredService<UpdateCheckService>().GetUpdateInfo());

    app.MapHub<ManualControlHub>("/hub/manual");
    app.MapHub<ConnectionHub>("/hub/connection");
    app.MapHub<InfoHub>("/hub/info");
    app.MapHub<ProgramSettingsHub>("/hub/programSettings");
    app.MapHub<SeatSettingsHub>("/hub/seatSettings");
    app.MapHub<TelemetryHub>("/hub/telemetry");

    // Open the UI in its own chrome-less app window — but not when we were relaunched by a
    // self-update: the existing window reloads itself onto this new backend, so a second
    // window would be noise.
    if (!app.Environment.IsDevelopment() && !args.Contains(SelfUpdateService.UpdatedRelaunchArg))
    {
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        Task.Factory.StartNew(async () =>
        {
            await Task.Delay(1000);
            OpenAppWindow("http://localhost:5000", lifetime);
        });
    }
    app.Run();
}
catch (Exception e) when (IsAddressInUse(e))
{
    // Lost the startup race for port 5000 to another instance — surface its UI and exit
    // quietly instead of showing a crash.
    SurfaceRunningInstance();
}
catch (Exception e)
{
    // The published exe has no console window, so also record fatal startup errors to a file
    // next to the exe — otherwise they'd be invisible.
    System.Console.WriteLine(e);
    try
    {
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "speedseat-crash.log"), $"[{DateTime.Now:s}] {e}\n\n");
    }
    catch { /* logging the crash must never throw */ }
    System.Console.WriteLine("Press enter to close window");
    Console.ReadLine();
}

// True if some process is already listening on the given local TCP port.
bool IsPortInUse(int port)
{
    try
    {
        return System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
    }
    catch
    {
        return false;
    }
}

// True if the exception chain is a "port already in use" socket error.
bool IsAddressInUse(Exception e)
{
    for (Exception? x = e; x != null; x = x.InnerException)
        if (x is System.Net.Sockets.SocketException se && se.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
            return true;
    return false;
}

// Bring an already-running instance's UI to the front. In a normal (published) run this opens
// the app window; in Development a stray second launch just exits quietly.
void SurfaceRunningInstance()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        OpenAppWindowDetached("http://localhost:5000");
}

Stream GetConfigJSONStream()
{
    return Assembly.GetExecutingAssembly().GetManifestResourceStream("speedseat.config.json");
}

Stream GetAppsettingsJSONStream()
{
    return Assembly.GetExecutingAssembly().GetManifestResourceStream("speedseat.appsettings_template.json");
}

// Opens the frontend in a dedicated, chrome-less window and shuts the backend down when that
// window is closed (the launched browser instance is its own process — see LaunchAppWindow).
void OpenAppWindow(string url, IHostApplicationLifetime lifetime)
{
    var proc = LaunchAppWindow(url);
    if (proc != null)
    {
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => Environment.Exit(0); // close the window -> close the backend
    }
    else
    {
        OpenUrl(url); // no Chromium browser found — fall back to the default browser (not lifetime-bound)
    }
}

// Opens the UI window without tying the backend's lifetime to it. Used when another instance
// already owns the server: we just surface its UI and this process exits.
void OpenAppWindowDetached(string url)
{
    if (LaunchAppWindow(url) == null)
        OpenUrl(url);
}

// Launches Edge/Chrome in "--app" mode (no tabs, no address bar) with a dedicated
// user-data-dir, which forces a separate browser instance so the returned process stays alive
// until the window is closed. Returns null when no Chromium browser is available.
Process? LaunchAppWindow(string url)
{
    try
    {
        var browser = FindChromiumBrowser();
        if (browser == null)
            return null;

        var profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpeedSeat", "ui-profile");
        Directory.CreateDirectory(profileDir);

        var psi = new ProcessStartInfo(browser) { UseShellExecute = false };
        psi.ArgumentList.Add($"--app={url}");
        psi.ArgumentList.Add($"--user-data-dir={profileDir}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        return Process.Start(psi);
    }
    catch (Exception e)
    {
        System.Console.WriteLine($"Could not open app window, falling back to the default browser: {e.Message}");
        return null;
    }
}

string? FindChromiumBrowser()
{
    var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
    var pfx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
    var candidates = new[]
    {
        Path.Combine(pfx86, "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
        Path.Combine(pfx86, "Google", "Chrome", "Application", "chrome.exe"),
    };
    return candidates.FirstOrDefault(File.Exists);
}

void OpenUrl(string url)
{
    try
    {
        Process.Start(url);
    }
    catch
    {
        // hack because of this: https://github.com/dotnet/corefx/issues/10361
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            throw;
        }
    }
}