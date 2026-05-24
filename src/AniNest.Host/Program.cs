using AniNest.Host.Composition;
using AniNest.Host.Endpoints;
using AniNest.Host.ErrorHandling;
using AniNest.Host.Logging;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var hostLogPath = builder.Configuration["AniNest:HostLogPath"]
                  ?? Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                      "AniNest",
                      "logs",
                      "host.log");

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
