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
    builder.Services.AddSingleton<IFrontendLogger, FrontendLogger>();
    builder.Services.AddTransient<ISerialPortConnectionFactory, SerialPortConnectionFactory>();

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

    // Check for new releases in the background; the frontend picks the result up via InfoHub.
    _ = Task.Run(() => app.Services.GetRequiredService<UpdateCheckService>().GetUpdateInfo());

    app.MapHub<ManualControlHub>("/hub/manual");
    app.MapHub<ConnectionHub>("/hub/connection");
    app.MapHub<InfoHub>("/hub/info");
    app.MapHub<ProgramSettingsHub>("/hub/programSettings");
    app.MapHub<SeatSettingsHub>("/hub/seatSettings");
    app.MapHub<TelemetryHub>("/hub/telemetry");

    // Open in browser
    if (!app.Environment.IsDevelopment())
    {
        Task.Factory.StartNew(async () =>
        {
            await Task.Delay(1000);
            OpenUrl(InfoHub.GetLocalIPAddress(false));
        });
    }
    app.Run();
}
catch (Exception e)
{
    System.Console.WriteLine(e);
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