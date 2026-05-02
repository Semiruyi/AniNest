using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.Messages;
using LocalPlayer.Model;
using LocalPlayer.View.Pages.Library;
using LocalPlayer.View.Pages.Player;

namespace LocalPlayer.ViewModel;

public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private bool? _savedTaskbarAutoHide;

    [ObservableProperty]
    private object? _currentPage;

    public ShellViewModel(IServiceProvider services)
    {
        _services = services;

        WeakReferenceMessenger.Default.Register<FolderSelectedMessage>(this, (_, m) =>
        {
            EnterPlayerPage();
            CurrentPage = _services.GetRequiredService<PlayerPage>();
            WeakReferenceMessenger.Default.Send(new LoadPlayerFolderMessage(m.Path, m.Name));
        });

        WeakReferenceMessenger.Default.Register<BackRequestedMessage>(this, (_, _) =>
        {
            LeavePlayerPage();
            CurrentPage = _services.GetRequiredService<MainPage>();
        });

        Application.Current.Exit += (_, _) => RestoreTaskbarIfNeeded();

        CurrentPage = _services.GetRequiredService<MainPage>();
    }

    private async void EnterPlayerPage()
    {
        if (TaskbarHelper.IsAutoHideEnabled)
            return;

        _savedTaskbarAutoHide = false;
        await TaskbarHelper.EnableAutoHideAsync();
    }

    private async void LeavePlayerPage()
    {
        if (_savedTaskbarAutoHide is null)
            return;

        if (_savedTaskbarAutoHide == false)
            await TaskbarHelper.DisableAutoHideAsync();

        _savedTaskbarAutoHide = null;
    }

    private void RestoreTaskbarIfNeeded()
    {
        if (_savedTaskbarAutoHide is not false)
            return;

        TaskbarHelper.DisableAutoHide();
        _savedTaskbarAutoHide = null;
    }
}
