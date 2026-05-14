using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AniNest.CompositionRoot;
using AniNest.Features.Library;
using AniNest.Features.Player;
using AniNest.Features.Shell;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Presentation;
using AniNest.View;

namespace AniNest;

public partial class App : Application
{
    private static readonly Logger Log = AppLog.For<App>();
    private int _exitHandled;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Info("Application startup begin");

        var services = new ServiceCollection();
        ServiceRegistration.AddAniNestServices(services);
        var provider = services.BuildServiceProvider();
        Log.Info("Service provider built");

        provider.GetRequiredService<IApplicationRuntime>().Start();

        ApplicationExceptionHandler.Configure(this);
        Log.Info("Application exception handlers configured");

        Exit += (_, _) =>
        {
            if (Interlocked.Exchange(ref _exitHandled, 1) != 0)
                return;

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
                provider.GetRequiredService<IApplicationRuntime>().Stop();
            }
            catch (Exception ex)
            {
                Log.Error("Application runtime stop failed during exit", ex);
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
}



