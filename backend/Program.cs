using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

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

Stream GetConfigJSONStream()
{
    return Assembly.GetExecutingAssembly().GetManifestResourceStream("speedseat.config.json");
}

Stream GetAppsettingsJSONStream()
{
    return Assembly.GetExecutingAssembly().GetManifestResourceStream("speedseat.appsettings_template.json");
}

// Opens the frontend in a dedicated, chrome-less window (Edge/Chrome "--app" mode: no tabs,
// no address bar) running its own isolated browser instance. Because that instance is its own
// process, closing the window shuts the backend down too. Falls back to the default browser
// (no window/lifetime binding) when no Chromium browser is found.
void OpenAppWindow(string url, IHostApplicationLifetime lifetime)
{
    try
    {
        var browser = FindChromiumBrowser();
        if (browser != null)
        {
            // A dedicated user-data-dir forces a separate browser instance, so the launched
            // process stays alive until the window is closed (instead of handing off to an
            // already-running browser and exiting immediately).
            var profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpeedSeat", "ui-profile");
            Directory.CreateDirectory(profileDir);

            var psi = new ProcessStartInfo(browser) { UseShellExecute = false };
            psi.ArgumentList.Add($"--app={url}");
            psi.ArgumentList.Add($"--user-data-dir={profileDir}");
            psi.ArgumentList.Add("--no-first-run");
            psi.ArgumentList.Add("--no-default-browser-check");

            var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.EnableRaisingEvents = true;
                proc.Exited += (_, _) => Environment.Exit(0); // close the window -> close the backend
                return;
            }
        }
    }
    catch (Exception e)
    {
        System.Console.WriteLine($"Could not open app window, falling back to the default browser: {e.Message}");
    }

    OpenUrl(url);
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