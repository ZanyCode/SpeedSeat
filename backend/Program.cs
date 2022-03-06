using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
var provider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");
System.Console.WriteLine(provider.GetDirectoryContents("wwwroot").Exists);
System.Console.WriteLine(provider.GetFileInfo("index.html").Exists);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot")
});

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async http => { http.Response.Redirect("/index.html"); });
});

app.Run();