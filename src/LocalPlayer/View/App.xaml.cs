using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.CompositionRoot;
using LocalPlayer.Features.Library;
using LocalPlayer.Features.Player;
using LocalPlayer.Features.Shell;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.View;

namespace LocalPlayer;

public partial class App : Application
{
    private static readonly Logger Log = AppLog.For<App>();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ServiceRegistration.AddLocalPlayerServices(services);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<ISettingsService>().Load();
        provider.GetRequiredService<ILocalizationService>().SetLanguage(settings.Language);
        _ = provider.GetRequiredService<IMediaPlayerController>().WarmupAsync();

        ApplicationExceptionHandler.Configure(this);

        Exit += (_, _) =>
        {
            provider.GetRequiredService<PlayerViewModel>().CleanupCommand.Execute(null);
            provider.GetRequiredService<MainPageViewModel>().Cleanup();
            provider.Dispose();
        };

        provider.GetRequiredService<MainWindow>().Show();
    }
}



