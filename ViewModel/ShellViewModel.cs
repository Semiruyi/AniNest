using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.Messages;
using LocalPlayer.Model;
using LocalPlayer.Localization;
using LocalPlayer.View.Pages.Library;
using LocalPlayer.View.Pages.Player;

namespace LocalPlayer.ViewModel;

public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ILocalizationService _loc;
    private bool? _savedTaskbarAutoHide;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isFilePopupOpen;

    [ObservableProperty]
    private bool _isSettingsPopupOpen;

    [ObservableProperty]
    private bool _isLanguageSubmenuOpen;

    [ObservableProperty]
    private bool _isFullscreenAnimationSubmenuOpen;

    [ObservableProperty]
    private string _currentLanguageCode = "zh-CN";

    [ObservableProperty]
    private string _currentAnimationCode = "continuous";

    public IReadOnlyList<LanguageInfo> AvailableLanguages => _loc.AvailableLanguages;

    public ShellViewModel(IServiceProvider services, ILocalizationService loc)
    {
        _services = services;
        _loc = loc;
        _currentLanguageCode = _loc.CurrentLanguage;
        _currentAnimationCode = _services.GetRequiredService<ISettingsService>().Load().FullscreenAnimation;

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
    {
        IsSettingsPopupOpen = false;
        IsFilePopupOpen = !IsFilePopupOpen;
    }

    [RelayCommand]
    private void OpenSettingsPopup()
    {
        IsFilePopupOpen = false;
        IsSettingsPopupOpen = !IsSettingsPopupOpen;
        if (!IsSettingsPopupOpen)
        {
            IsLanguageSubmenuOpen = false;
            IsFullscreenAnimationSubmenuOpen = false;
        }
    }

    [RelayCommand]
    private void ToggleLanguageSubmenu()
    {
        IsLanguageSubmenuOpen = !IsLanguageSubmenuOpen;
    }

    [RelayCommand]
    private void SwitchLanguage(string code)
    {
        _loc.SetLanguage(code);
        CurrentLanguageCode = _loc.CurrentLanguage;
        var settings = _services.GetRequiredService<ISettingsService>();
        var s = settings.Load();
        s.Language = code;
        settings.Save();
    }

    [RelayCommand]
    private void ToggleFullscreenAnimationSubmenu()
    {
        IsFullscreenAnimationSubmenuOpen = !IsFullscreenAnimationSubmenuOpen;
    }

    [RelayCommand]
    private void SelectFullscreenAnimation(string code)
    {
        CurrentAnimationCode = code;
        var settings = _services.GetRequiredService<ISettingsService>();
        var s = settings.Load();
        s.FullscreenAnimation = code;
        settings.Save();
    }

    [RelayCommand]
    private void AddFolder()
    {
        IsFilePopupOpen = false;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = _loc["Dialog.SelectFolder"]
        };

        if (dialog.ShowDialog() != true) return;

        string path = dialog.FolderName;
        string name = Path.GetFileName(path);
        var settings = _services.GetRequiredService<ISettingsService>();

        if (settings.GetFolders().Any(f => f.Path == path))
        {
            MessageBox.Show(_loc["Dialog.FolderAlreadyAdded"], _loc["Dialog.Info"]);
            return;
        }

        var (count, coverPath) = VideoScanner.ScanFolder(path);
        if (count == 0)
        {
            MessageBox.Show(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
            return;
        }

        var (success, error) = settings.AddFolder(path, name);
        if (!success)
        {
            MessageBox.Show(error ?? _loc["Dialog.UnknownError"], _loc["Dialog.Error"]);
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
