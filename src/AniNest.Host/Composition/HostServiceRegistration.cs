using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Application.Settings;
using AniNest.Application.Thumbnail;
using AniNest.Host.Events;
using AniNest.Host.Modules;
using AniNest.Host.Modules.Resources;

namespace AniNest.Host.Composition;

internal static class HostServiceRegistration
{
    public static IServiceCollection AddAniNestHostServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedServices();
        services.AddLibraryServices(configuration);
        services.AddPlaybackServices(configuration);
        services.AddThumbnailServices(configuration);
        services.AddSettingsServices(configuration);
        services.AddMetadataServices(configuration);
        services.AddResourceServices(configuration);

        return services;
    }

    private static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IHostEventStream, InMemoryHostEventStream>();
        services.AddSingleton<IResourceUrlService, ResourceUrlService>();
        return services;
    }

    private static IServiceCollection AddLibraryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ILibraryFileScanner, FileSystemLibraryFileScanner>();
        services.AddSingleton<ILibraryCatalogStore>(_ => new FileLibraryCatalogStore(
            ResolvePath(configuration, "AniNest:LibraryCatalogPath", "library-catalog.json"),
            LibraryCatalogDefaults.CreateFolders(),
            LibraryCatalogDefaults.CreateWatchStatuses(),
            LibraryCatalogDefaults.CreateFavorites()));
        services.AddSingleton<LibraryCatalogService>();
        services.AddSingleton<LibraryFolderProjection>();
        services.AddSingleton<ILibraryModule, LibraryModule>();
        return services;
    }

    private static IServiceCollection AddPlaybackServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IPlaybackProgressStore>(_ => new FilePlaybackProgressStore(
            ResolvePath(configuration, "AniNest:PlaybackProgressPath", "playback-progress.json"),
            PlaybackProgressDefaults.CreateVideoProgress(),
            PlaybackProgressDefaults.CreateFolderProgress()));
        services.AddSingleton<PlaybackProgressSummaryService>();
        services.AddSingleton<IPlaylistCatalogStore, FileSystemPlaylistCatalogStore>();
        services.AddSingleton<PlaylistCatalogService>();
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>().Load().Player;
            return new PlaybackSessionEngine(
                sp.GetRequiredService<PlaylistCatalogService>(),
                settings,
                sp.GetRequiredService<IPlaybackProgressStore>(),
                sp.GetRequiredService<IResourceUrlService>());
        });
        services.AddSingleton<PlaybackModule>();
        services.AddSingleton<IPlaylistModule>(sp => sp.GetRequiredService<PlaybackModule>());
        services.AddSingleton<ISessionModule>(sp => sp.GetRequiredService<PlaybackModule>());
        return services;
    }

    private static IServiceCollection AddThumbnailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IThumbnailStore>(_ => new FileThumbnailStore(
            ResolvePath(configuration, "AniNest:ThumbnailPath", "thumbnails.json"),
            ThumbnailDefaults.Create()));
        services.AddSingleton<ThumbnailService>();
        services.AddSingleton<ThumbnailFolderProjection>();
        services.AddSingleton<IThumbnailModule, ThumbnailModule>();
        return services;
    }

    private static IServiceCollection AddSettingsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ISettingsStore>(_ => new FileSettingsStore(
            ResolvePath(configuration, "AniNest:SettingsPath", "host-settings.json"),
            SettingsDefaults.Create()));
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsModule, SettingsModule>();
        return services;
    }

    private static IServiceCollection AddMetadataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IMetadataStore>(_ => new FileMetadataStore(
            ResolvePath(configuration, "AniNest:MetadataPath", "metadata.json"),
            MetadataDefaults.Create()));
        services.AddSingleton<IMetadataRecordStore>(_ => new FileMetadataRecordStore(
            ResolvePath(configuration, "AniNest:MetadataIndexPath", Path.Combine("metadata", "index.json")),
            MetadataStorageDefaults.CreateRecords()));
        services.AddSingleton<IMetadataReviewStore>(_ => new FileMetadataReviewStore(
            ResolvePath(configuration, "AniNest:MetadataReviewPath", Path.Combine("metadata", "review.json"))));
        services.AddSingleton<IMetadataPayloadRepository>(_ => new FileMetadataPayloadRepository(
            ResolvePath(configuration, "AniNest:MetadataPayloadRootPath", Path.Combine("metadata", "payload"))));
        services.AddSingleton<IMetadataPosterCache>(_ => new FileMetadataPosterCache(
            ResolvePath(configuration, "AniNest:MetadataPosterRootPath", Path.Combine("metadata", "posters"))));
        services.AddSingleton<IMetadataAssetService, MetadataAssetService>();
        services.AddSingleton<IMetadataProjectionService, MetadataProjectionService>();
        services.AddSingleton<IMetadataRuntimeBootstrapService, MetadataRuntimeBootstrapService>();
        services.AddSingleton<IMetadataReviewService, MetadataReviewService>();
        services.AddSingleton<IMetadataOrchestrationService, MetadataOrchestrationService>();
        services.AddSingleton<IMetadataPreparationService, MetadataPreparationService>();
        services.AddSingleton<IMetadataAcquisitionService, MetadataAcquisitionService>();
        services.AddSingleton<IMetadataConfidenceService, MetadataConfidenceService>();
        services.AddSingleton<IMetadataResolutionService, MetadataResolutionService>();
        services.AddSingleton<IMetadataFetchPipeline, MetadataFetchPipeline>();
        services.AddSingleton<IMetadataTaskPlanner, MetadataTaskPlanner>();
        services.AddSingleton<IMetadataTaskQueue, MetadataTaskQueue>();
        services.AddSingleton<IMetadataTaskScheduler, MetadataTaskScheduler>();
        services.AddSingleton<IMetadataRuntimeStateService, MetadataRuntimeStateService>();
        services.AddSingleton<IMetadataLifecycleService, MetadataLifecycleService>();
        services.AddSingleton<IMetadataModule, MetadataModule>();
        if (configuration.GetValue("AniNest:MetadataWorkerEnabled", true))
            services.AddHostedService<MetadataBackgroundService>();
        services.AddHttpClient<IAnimeMetadataProvider, BangumiMetadataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.bgm.tv/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }

    private static IServiceCollection AddResourceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IResourceLocator>(sp => new ResourceLocator(
            sp.GetRequiredService<ILibraryCatalogStore>(),
            sp.GetRequiredService<IMetadataStore>(),
            sp.GetRequiredService<IPlaylistCatalogStore>(),
            ResolvePath(configuration, "AniNest:MetadataPosterRootPath", Path.Combine("metadata", "posters"))));
        return services;
    }

    private static string ResolvePath(IConfiguration configuration, string key, string fileName)
    {
        var configured = configuration[key];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", fileName));
    }
}
