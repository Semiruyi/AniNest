using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AniNest.CompositionRoot;
using AniNest.Features.Library;
using AniNest.Features.Player;
using AniNest.Features.Shell;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Persistence;
using AniNest.View;

namespace AniNest;

public partial class App : Application
{
    private static readonly Logger Log = AppLog.For<App>();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ServiceRegistration.AddAniNestServices(services);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<ISettingsService>().Load();
        provider.GetRequiredService<ILocalizationService>().SetLanguage(settings.Language);
        _ = provider.GetRequiredService<IMediaPlayerController>().WarmupAsync();

        ApplicationExceptionHandler.Configure(this);

        Exit += (_, _) =>
        {
            provider.GetRequiredService<PlayerViewModel>().CleanupCommand.Execute(null);
            provider.GetRequiredService<MainPageViewModel>().Cleanup();
            provider.GetRequiredService<ISettingsService>().Save();
            provider.Dispose();
            PerfLogger.Shutdown(TimeSpan.FromSeconds(1));
            AppLog.Shutdown(TimeSpan.FromSeconds(1));
        };

        provider.GetRequiredService<MainWindow>().Show();
    }
}



