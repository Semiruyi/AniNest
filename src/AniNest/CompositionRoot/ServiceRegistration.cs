using Microsoft.Extensions.DependencyInjection;
using AniNest.Features.Library;
using AniNest.Features.Library.Services;
using AniNest.Features.Player;
using AniNest.Features.Player.Services;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Settings;
using AniNest.Features.Shell.Services;
using AniNest.Features.Shell;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
using AniNest.View;

namespace AniNest.CompositionRoot;

public static class ServiceRegistration
{
    public static void AddAniNestServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<ITaskbarAutoHideCoordinator, TaskbarAutoHideCoordinator>();
        services.AddSingleton<IVideoScanner, VideoScanner>();
        services.AddSingleton<IThumbnailDecodeStrategyService, ThumbnailDecodeStrategyService>();
        services.AddSingleton<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddSingleton<ILibraryAppService, LibraryAppService>();
        services.AddSingleton<IShellPreferencesService, ShellPreferencesService>();
        services.AddSingleton<IShellSettingsAppService, ShellSettingsAppService>();
        services.AddSingleton<IShellThumbnailPerformanceAppService, ShellThumbnailPerformanceAppService>();
        services.AddSingleton<IPlayerAppService, PlayerAppService>();
        services.AddSingleton<IPlayerPlaybackFacade, PlayerPlaybackFacade>();
        services.AddSingleton<IPlayerPlaybackStateSyncService, PlayerPlaybackStateSyncService>();
        services.AddSingleton<IPlayerPlaylistService, PlayerPlaylistService>();
        services.AddSingleton<IPlayerThumbnailSyncService, PlayerThumbnailSyncService>();
        services.AddSingleton<PlayerSessionController>();
        services.AddSingleton<PlayerPlaybackStateController>();
        services.AddSingleton<IPlayerInputService, PlayerInputService>();
        services.AddSingleton<IMediaPlayerController, MediaPlayerController>();

        services.AddSingleton<MainPageViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<PlayerInputSettingsViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
    }
}



