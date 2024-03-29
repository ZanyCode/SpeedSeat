using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

try
{
    bool configValid = RestoreConfigFile();
    var builder = WebApplication.CreateBuilder(args);

    if (configValid)
        builder.Configuration.AddJsonFile("config.json", false, true);

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

Stream GetAppsettingsJSONStream()
{
    return Assembly.GetExecutingAssembly().GetManifestResourceStream("speedseat.appsettings_template.json");
}

bool RestoreConfigFile()
{
    if (!File.Exists("config.json"))
    {
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("speedseat.config_template.json"))
        {
            using (var file = new FileStream("config.json", FileMode.Create, FileAccess.Write))
            {
                resource.CopyTo(file);
            }
        }
    }

    return ValidateConfigFile() == null;
}

string ValidateConfigFile()
{
    if (File.Exists("config.json"))
    {
        var json = File.ReadAllText("config.json");
        try
        {
            JsonSerializer.Deserialize<Config>(json);
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
    else
        return "File does not exist";
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