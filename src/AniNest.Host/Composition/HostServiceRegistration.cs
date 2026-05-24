using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Application.Settings;
using AniNest.Application.Thumbnail;
using AniNest.Contracts.Settings;
using AniNest.Host.Events;
using AniNest.Host.Modules;
using AniNest.Host.Modules.Resources;

namespace AniNest.Host.Composition;

internal static class HostServiceRegistration
{
    public static IServiceCollection AddAniNestHostServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IHostEventStream, InMemoryHostEventStream>();
        services.AddSingleton<IResourceUrlService, ResourceUrlService>();

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
        services.AddSingleton<IMetadataRecordStore>(_ => new FileMetadataRecordStore(
            ResolvePath(configuration, "AniNest:MetadataIndexPath", Path.Combine("metadata", "index.json")),
            MetadataStorageDefaults.CreateRecords()));
        services.AddSingleton<IMetadataPayloadRepository>(_ => new FileMetadataPayloadRepository(
            ResolvePath(configuration, "AniNest:MetadataPayloadRootPath", Path.Combine("metadata", "payload"))));
        services.AddSingleton<IMetadataPosterCache>(_ => new FileMetadataPosterCache(
            ResolvePath(configuration, "AniNest:MetadataPosterRootPath", Path.Combine("metadata", "posters"))));
        services.AddSingleton<IMetadataTaskScheduler, MetadataTaskScheduler>();
        services.AddSingleton<IMetadataLifecycleService, MetadataLifecycleService>();
        services.AddSingleton<IResourceLocator, ResourceLocator>();

        services.AddSingleton<ISettingsStore>(_ => new FileSettingsStore(
            ResolvePath(configuration, "AniNest:SettingsPath", "host-settings.json"),
            SettingsDefaults.Create()));

        services.AddSingleton<ISettingsModule, SettingsModule>();
        services.AddSingleton<IMetadataModule, MetadataModule>();
        services.AddSingleton<IThumbnailModule, ThumbnailModule>();
        if (configuration.GetValue("AniNest:MetadataWorkerEnabled", true))
            services.AddHostedService<MetadataBackgroundService>();
        services.AddHttpClient<IAnimeMetadataProvider, BangumiMetadataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.bgm.tv/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<Func<MetadataSettingsDto>>(sp =>
        {
            var module = sp.GetRequiredService<ISettingsModule>();
            return () => module.GetMetadataAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    private static string ResolvePath(IConfiguration configuration, string key, string fileName)
        => configuration[key]
           ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "AniNest",
               fileName);
}
