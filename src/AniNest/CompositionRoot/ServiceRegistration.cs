using Microsoft.Extensions.DependencyInjection;
using AniNest.Features.Library;
using AniNest.Features.Player;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Settings;
using AniNest.Features.Shell;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
using AniNest.View;

namespace AniNest.CompositionRoot;

public static class ServiceRegistration
{
    public static void AddAniNestServices(IServiceCollection services)
    {
        AniNestAppServiceRegistration.AddServices(services);
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IApplicationLifecycle, WpfApplicationLifecycle>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IFolderPickerService, WpfFolderPickerService>();
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<ITaskbarAutoHideCoordinator, TaskbarAutoHideCoordinator>();
        services.AddSingleton<IThumbnailDecodeStrategyService, ThumbnailDecodeStrategyService>();
        services.AddSingleton<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddSingleton<MediaPlayerController>();
        services.AddSingleton<IMediaPlayerController>(sp => sp.GetRequiredService<MediaPlayerController>());
        services.AddSingleton<IWpfVideoSurfaceSource, WpfVideoSurfaceSource>();

        services.AddSingleton<MainPageViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
    }
}



