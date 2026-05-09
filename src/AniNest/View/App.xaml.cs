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
        Log.Info("Application startup begin");

        var services = new ServiceCollection();
        ServiceRegistration.AddAniNestServices(services);
        var provider = services.BuildServiceProvider();
        Log.Info("Service provider built");

        var settings = provider.GetRequiredService<ISettingsService>().Load();
        Log.Info($"Settings loaded. language={settings.Language}");
        provider.GetRequiredService<ILocalizationService>().SetLanguage(settings.Language);
        Log.Info($"Localization applied. language={settings.Language}");
        _ = WarmupMediaAsync(provider.GetRequiredService<IMediaPlayerController>());

        ApplicationExceptionHandler.Configure(this);
        Log.Info("Application exception handlers configured");

        Exit += (_, _) =>
        {
            Log.Info("Application exit begin");
            try
            {
                provider.GetRequiredService<PlayerViewModel>().CleanupCommand.Execute(null);
                Log.Info("PlayerViewModel cleanup complete");
            }
            catch (Exception ex)
            {
                Log.Error("PlayerViewModel cleanup failed", ex);
            }

            try
            {
                provider.GetRequiredService<MainPageViewModel>().Cleanup();
                Log.Info("MainPageViewModel cleanup complete");
            }
            catch (Exception ex)
            {
                Log.Error("MainPageViewModel cleanup failed", ex);
            }

            try
            {
                provider.GetRequiredService<ISettingsService>().Save();
                Log.Info("Settings save complete");
            }
            catch (Exception ex)
            {
                Log.Error("Settings save failed during exit", ex);
            }

            try
            {
                provider.Dispose();
                Log.Info("Service provider disposed");
            }
            catch (Exception ex)
            {
                Log.Error("Service provider dispose failed", ex);
            }

            try
            {
                PerfLogger.Shutdown(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                Log.Error("PerfLogger shutdown failed", ex);
            }

            Log.Info("Application exit complete");
            AppLog.Shutdown(TimeSpan.FromSeconds(1));
        };

        provider.GetRequiredService<MainWindow>().Show();
        Log.Info("Main window shown");
    }

    private static async Task WarmupMediaAsync(IMediaPlayerController mediaPlayerController)
    {
        Log.Info("Media warmup queued");

        try
        {
            await mediaPlayerController.WarmupAsync();
            Log.Info("Media warmup complete");
        }
        catch (Exception ex)
        {
            Log.Error("Media warmup failed", ex);
        }
    }
}



