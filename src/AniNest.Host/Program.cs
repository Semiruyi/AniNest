using AniNest.Application.Modules;
using AniNest.Host.Endpoints;
using AniNest.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILibraryModule, InMemoryLibraryModule>();
builder.Services.AddSingleton<ISessionModule, InMemorySessionModule>();
builder.Services.AddSingleton<ISettingsModule, InMemorySettingsModule>();

var app = builder.Build();

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
