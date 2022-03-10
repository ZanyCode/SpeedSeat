using System.IO.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors();
// builder.Services.AddSingleton<SpeedseatSettings>();
builder.Services.AddSingleton<Speedseat>();
builder.Services.AddDbContext<SpeedseatContext>(options => options.UseSqlite("Data Source=speedseat.sqlite3"));

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
app.Run();