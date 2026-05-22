using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Application.Settings;
using AniNest.Application.Thumbnail;
using AniNest.Host.Events;
using AniNest.Host.Modules;

namespace AniNest.Host.Composition;

internal static class HostServiceRegistration
{
    public static IServiceCollection AddAniNestHostServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IHostEventStream, InMemoryHostEventStream>();

        services.AddSingleton<ILibraryModule, LibraryModule>();
        services.AddSingleton<ILibraryFileScanner, FileSystemLibraryFileScanner>();
        services.AddSingleton<ILibraryCatalogStore>(_ => new FileLibraryCatalogStore(
            ResolvePath(configuration, "AniNest:LibraryCatalogPath", "library-catalog.json"),
            LibraryCatalogDefaults.CreateFolders(),
            LibraryCatalogDefaults.CreateWatchStatuses(),
            LibraryCatalogDefaults.CreateFavorites()));

        services.AddSingleton<IPlaybackProgressStore>(_ => new FilePlaybackProgressStore(
            ResolvePath(configuration, "AniNest:PlaybackProgressPath", "playback-progress.json"),
            PlaybackProgressDefaults.CreateVideoProgress(),
            PlaybackProgressDefaults.CreateFolderProgress()));

        services.AddSingleton<IPlaylistCatalogStore, FileSystemPlaylistCatalogStore>();
        services.AddSingleton<IThumbnailStore>(_ => new FileThumbnailStore(
            ResolvePath(configuration, "AniNest:ThumbnailPath", "thumbnails.json"),
            ThumbnailDefaults.Create()));

        services.AddSingleton<PlaybackModule>();
        services.AddSingleton<IPlaylistModule>(sp => sp.GetRequiredService<PlaybackModule>());
        services.AddSingleton<ISessionModule>(sp => sp.GetRequiredService<PlaybackModule>());

        services.AddSingleton<IMetadataStore>(_ => new FileMetadataStore(
            ResolvePath(configuration, "AniNest:MetadataPath", "metadata.json"),
            MetadataDefaults.Create()));

        services.AddSingleton<ISettingsStore>(_ => new FileSettingsStore(
            ResolvePath(configuration, "AniNest:SettingsPath", "host-settings.json"),
            SettingsDefaults.Create()));

        services.AddSingleton<ISettingsModule, SettingsModule>();
        services.AddSingleton<IMetadataModule, MetadataModule>();
        services.AddSingleton<IThumbnailModule, ThumbnailModule>();

        return services;
    }

    private static string ResolvePath(IConfiguration configuration, string key, string fileName)
        => configuration[key]
           ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "AniNest",
               fileName);
}
