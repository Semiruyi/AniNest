using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.CompositionRoot;
using LocalPlayer.Features.Library;
using LocalPlayer.Features.Player;
using LocalPlayer.Features.Shell;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
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

        ConfigureExceptionHandling();

        Exit += (_, _) =>
        {
            provider.GetRequiredService<PlayerViewModel>().CleanupCommand.Execute(null);
            provider.GetRequiredService<MainPageViewModel>().Cleanup();
            provider.Dispose();
        };

        provider.GetRequiredService<MainWindow>().Show();
    }

    private void ConfigureExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Dispatcher unhandled exception", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (args.Exception != null)
                Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }
}



