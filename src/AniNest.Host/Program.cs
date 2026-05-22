using AniNest.Application.Modules;
using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Settings;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Application.Thumbnail;
using AniNest.Host.Endpoints;
using AniNest.Host.ErrorHandling;
using AniNest.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILibraryModule, LibraryModule>();
builder.Services.AddSingleton<ILibraryCatalogStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var catalogPath = configuration["AniNest:LibraryCatalogPath"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniNest",
            "library-catalog.json");

    return new FileLibraryCatalogStore(
        catalogPath,
        LibraryCatalogDefaults.CreateFolders(),
        LibraryCatalogDefaults.CreateWatchStatuses(),
        LibraryCatalogDefaults.CreateFavorites());
});
builder.Services.AddSingleton<IPlaybackProgressStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var progressPath = configuration["AniNest:PlaybackProgressPath"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniNest",
            "playback-progress.json");

    return new FilePlaybackProgressStore(
        progressPath,
        PlaybackProgressDefaults.CreateVideoProgress(),
        PlaybackProgressDefaults.CreateFolderProgress());
});
builder.Services.AddSingleton<IPlaylistCatalogStore, FileSystemPlaylistCatalogStore>();
builder.Services.AddSingleton<IThumbnailStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var thumbnailPath = configuration["AniNest:ThumbnailPath"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniNest",
            "thumbnails.json");

    return new FileThumbnailStore(thumbnailPath, ThumbnailDefaults.Create());
});
builder.Services.AddSingleton<PlaybackModule>();
builder.Services.AddSingleton<IPlaylistModule>(sp => sp.GetRequiredService<PlaybackModule>());
builder.Services.AddSingleton<ISessionModule>(sp => sp.GetRequiredService<PlaybackModule>());
builder.Services.AddSingleton<IMetadataStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var metadataPath = configuration["AniNest:MetadataPath"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniNest",
            "metadata.json");

    return new FileMetadataStore(metadataPath, MetadataDefaults.Create());
});
builder.Services.AddSingleton<ISettingsStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var settingsPath = configuration["AniNest:SettingsPath"]
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniNest",
            "host-settings.json");

    return new FileSettingsStore(settingsPath, SettingsDefaults.Create());
});
builder.Services.AddSingleton<ISettingsModule, SettingsModule>();
builder.Services.AddSingleton<IMetadataModule, MetadataModule>();
builder.Services.AddSingleton<IThumbnailModule, ThumbnailModule>();

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
