using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.Features.Library;
using LocalPlayer.Features.Library.Services;
using LocalPlayer.Features.Player;
using LocalPlayer.Features.Player.Services;
using LocalPlayer.Features.Player.Input;
using LocalPlayer.Features.Player.Settings;
using LocalPlayer.Features.Shell;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.View;

namespace LocalPlayer.CompositionRoot;

public static class ServiceRegistration
{
    public static void AddLocalPlayerServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<ITaskbarAutoHideCoordinator, TaskbarAutoHideCoordinator>();
        services.AddSingleton<IVideoScanner, VideoScanner>();
        services.AddSingleton<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddSingleton<ILibraryAppService, LibraryAppService>();
        services.AddSingleton<IPlayerAppService, PlayerAppService>();
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



