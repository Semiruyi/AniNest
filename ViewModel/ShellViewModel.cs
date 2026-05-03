using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private bool _isFilePopupOpen;

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

    [RelayCommand]
    private void OpenFilePopup()
        => IsFilePopupOpen = true;

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择包含视频的文件夹"
        };

        if (dialog.ShowDialog() != true) return;

        string path = dialog.FolderName;
        string name = Path.GetFileName(path);
        var settings = _services.GetRequiredService<ISettingsService>();

        if (settings.GetFolders().Any(f => f.Path == path))
        {
            MessageBox.Show("该文件夹已添加", "提示");
            return;
        }

        var (count, coverPath) = VideoScanner.ScanFolder(path);
        if (count == 0)
        {
            MessageBox.Show("该文件夹内没有视频文件", "提示");
            return;
        }

        var (success, error) = settings.AddFolder(path, name);
        if (!success)
        {
            MessageBox.Show(error ?? "未知错误", "错误");
            return;
        }
        WeakReferenceMessenger.Default.Send(new FolderAddedMessage(name, path, count, coverPath));
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
