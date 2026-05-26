using AniNest.Host.Composition;
using AniNest.Host.Endpoints;
using AniNest.Host.ErrorHandling;
using AniNest.Host.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey)))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5275");
}

var hostLogPathSetting = builder.Configuration["AniNest:HostLogPath"];
var hostLogPath = string.IsNullOrWhiteSpace(hostLogPathSetting)
    ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "logs", "host.log"))
    : Path.IsPathRooted(hostLogPathSetting)
        ? hostLogPathSetting
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, hostLogPathSetting));

builder.Logging.AddProvider(new FileLoggerProvider(hostLogPath, LogLevel.Information));

builder.Services.AddAniNestHostServices(builder.Configuration);

var app = builder.Build();

app.UseApiExceptionHandling();
app.UseStaticFiles();
app.MapGet("/", () => Results.Redirect("/api/settings"));
app.MapLibraryEndpoints();
app.MapResourceEndpoints();
app.MapPlaylistEndpoints();
app.MapSessionEndpoints();
app.MapMetadataEndpoints();
app.MapThumbnailEndpoints();
app.MapSettingsEndpoints();
app.MapEventEndpoints();

app.Run();

public partial class Program;
