using AniNest.Application.Modules;
using AniNest.Host.Endpoints;
using AniNest.Host.ErrorHandling;
using AniNest.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILibraryModule, InMemoryLibraryModule>();
builder.Services.AddSingleton<InMemoryPlaybackCoordinator>();
builder.Services.AddSingleton<IPlaylistModule>(sp => sp.GetRequiredService<InMemoryPlaybackCoordinator>());
builder.Services.AddSingleton<ISessionModule>(sp => sp.GetRequiredService<InMemoryPlaybackCoordinator>());
builder.Services.AddSingleton<ISettingsModule, InMemorySettingsModule>();

var app = builder.Build();

app.UseApiExceptionHandling();
app.MapGet("/", () => Results.Redirect("/api/settings"));
app.MapLibraryEndpoints();
app.MapPlaylistEndpoints();
app.MapSessionEndpoints();
app.MapMetadataEndpoints();
app.MapThumbnailEndpoints();
app.MapSettingsEndpoints();
app.MapEventEndpoints();

app.Run();

public partial class Program;
