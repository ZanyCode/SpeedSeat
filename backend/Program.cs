using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors();
builder.Services.AddSingleton<SpeedseatSettings>();
builder.Services.AddSingleton<Speedseat>();
builder.Services.AddDbContext<SpeedseatContext>(options => options.UseSqlite("Data Source=speedseat.sqlite3"));
builder.Services.AddHttpContextAccessor();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // to listen for incoming http connection on port 5001
    options.ListenAnyIP(7001, configure => configure.UseHttps()); // to listen for incoming https connection on port 7001
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseCors(o => {
    o.AllowAnyHeader();
    o.AllowAnyMethod();
    o.AllowCredentials();
    o.SetIsOriginAllowed(origin => true);
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot")
});

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async http => { http.Response.Redirect("/index.html"); });
});
app.MapHub<ManualControlHub>("/manual");
app.MapHub<ConnectionHub>("/connection");
app.MapHub<InfoHub>("/info");
app.MapHub<SettingsHub>("/settings");

// Open in browser
if(!app.Environment.IsDevelopment()) {
    Task.Factory.StartNew(async () => {
        await Task.Delay(1000);
        OpenUrl(InfoHub.GetLocalIPAddress(false));
    });
}

app.Run();


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