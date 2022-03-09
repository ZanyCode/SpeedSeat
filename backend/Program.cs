using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors();

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
app.Run();