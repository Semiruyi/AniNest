using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.Model;
using LocalPlayer.View;
using LocalPlayer.View.Pages.Library;
using LocalPlayer.View.Pages.Player;
using LocalPlayer.ViewModel;
using LocalPlayer.ViewModel.Player;

namespace LocalPlayer;

public partial class App : Application
{
    private static readonly Logger Log = AppLog.For<App>();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        ConfigureExceptionHandling();

        Exit += (_, _) => provider.Dispose();

        provider.GetRequiredService<MainWindow>().Show();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddTransient<IMediaPlayerController, MediaPlayerController>();

        services.AddTransient<MainPageViewModel>();
        services.AddTransient<PlayerViewModel>();

        services.AddTransient<MainPage>();
        services.AddTransient<PlayerPage>();

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private void ConfigureExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Error( "未处理异常", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error( "Dispatcher未处理异常", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (args.Exception != null)
                Log.Error( "未观察到的Task异常", args.Exception);
            args.SetObserved();
        };
    }
}
