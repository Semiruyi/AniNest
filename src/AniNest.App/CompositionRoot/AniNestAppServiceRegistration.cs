using Microsoft.Extensions.DependencyInjection;
using AniNest.Features.Library.Services;
using AniNest.Features.Metadata;
using AniNest.Features.Player;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Settings;
using AniNest.Features.Player.Services;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.CompositionRoot;

public static class AniNestAppServiceRegistration
{
    public static void AddServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IApplicationRuntime, ApplicationRuntime>();
        services.AddSingleton<IMetadataRepository, MetadataRepository>();
        services.AddSingleton<MetadataIndexStore>();
        services.AddSingleton<MetadataTaskStore>();
        services.AddSingleton<MetadataMatcher>();
        services.AddSingleton<IMetadataImageCache, MetadataImageCache>();
        services.AddSingleton<MetadataEventHub>();
        services.AddSingleton<IMetadataEvents>(sp => sp.GetRequiredService<MetadataEventHub>());
        services.AddSingleton<IMetadataProvider, BangumiMetadataProvider>();
        services.AddSingleton<MetadataWorker>();
        services.AddSingleton<IMetadataQueryService, MetadataQueryService>();
        services.AddSingleton<IMetadataSyncService, MetadataSyncCoordinator>();
        services.AddSingleton<IVideoScanner, VideoScanner>();
        services.AddSingleton<ILibraryThumbnailService, LibraryThumbnailService>();
        services.AddSingleton<ILibraryTrackingService, LibraryTrackingService>();
        services.AddSingleton<ILibraryAppService, LibraryAppService>();
        services.AddSingleton<IShellPreferencesService, ShellPreferencesService>();
        services.AddSingleton<IShellSettingsAppService, ShellSettingsAppService>();
        services.AddSingleton<IShellThumbnailPerformanceAppService, ShellThumbnailPerformanceAppService>();
        services.AddSingleton<IPlayerInputService, PlayerInputService>();
        services.AddSingleton<IPlayerAppService, PlayerAppService>();
        services.AddSingleton<IPlayerPlaybackFacade, PlayerPlaybackFacade>();
        services.AddSingleton<IPlayerPlaybackStateSyncService, PlayerPlaybackStateSyncService>();
        services.AddSingleton<IPlayerPlaylistService, PlayerPlaylistService>();
        services.AddSingleton<IPlayerThumbnailSyncService, PlayerThumbnailSyncService>();
        services.AddSingleton<PlayerSessionController>();
        services.AddSingleton<PlayerPlaybackStateController>();
        services.AddSingleton<PlayerInputSettingsViewModel>();
    }
}
