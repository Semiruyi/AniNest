using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Features.Library;
using LocalPlayer.Features.Player;
using LocalPlayer.Core.Localization;
using LocalPlayer.Core.Messaging;
using LocalPlayer.Infrastructure.Model;

namespace LocalPlayer.Features.Shell;

public partial class ShellViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private readonly MainPageViewModel _mainPage;
    private readonly PlayerViewModel _playerPage;
    private bool? _savedTaskbarAutoHide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnMainPage))]
    private object? _currentPage;

    public bool IsOnMainPage => CurrentPage is MainPageViewModel;

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

    public ShellViewModel(
        ISettingsService settings,
        ILocalizationService loc,
        MainPageViewModel mainPage,
        PlayerViewModel playerPage)
    {
        _settings = settings;
        _loc = loc;
        _currentLanguageCode = _loc.CurrentLanguage;
        _currentAnimationCode = _settings.Load().FullscreenAnimation;
        _mainPage = mainPage;
        _playerPage = playerPage;

        WeakReferenceMessenger.Default.Register<FolderSelectedMessage>(this, (_, m) =>
        {
            EnterPlayerPage();
            CurrentPage = _playerPage;
            WeakReferenceMessenger.Default.Send(new LoadPlayerFolderSkeletonMessage(m.Path, m.Name));
        });

        WeakReferenceMessenger.Default.Register<BackRequestedMessage>(this, (_, _) =>
        {
            LeavePlayerPage();
            CurrentPage = _mainPage;
        });

        Application.Current.Exit += (_, _) => RestoreTaskbarIfNeeded();

        CurrentPage = _mainPage;
    }

    [RelayCommand]
    private void OpenFilePopup()
    {
        IsSettingsPopupOpen = false;
        IsFilePopupOpen = !IsFilePopupOpen;
    }

    partial void OnIsSettingsPopupOpenChanged(bool value)
    {
        if (!value)
        {
            IsLanguageSubmenuOpen = false;
            IsFullscreenAnimationSubmenuOpen = false;
        }
    }

    [RelayCommand]
    private void OpenSettingsPopup()
    {
        IsFilePopupOpen = false;
        IsSettingsPopupOpen = !IsSettingsPopupOpen;
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
        var s = _settings.Load();
        s.Language = code;
        _settings.Save();
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
        var s = _settings.Load();
        s.FullscreenAnimation = code;
        _settings.Save();
    }

    [RelayCommand]
    private void AddFolder()
    {
        IsFilePopupOpen = false;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = _loc["Dialog.AddFolder"]
        };

        if (dialog.ShowDialog() != true) return;

        string path = dialog.FolderName;
        string name = Path.GetFileName(path);

        if (_settings.GetFolders().Any(f => f.Path == path))
        {
            MessageBox.Show(_loc["Dialog.FolderAlreadyAdded"], _loc["Dialog.Info"]);
            return;
        }

        var scanResult = VideoScanner.ScanFolder(path);
        if (scanResult.VideoCount == 0)
        {
            MessageBox.Show(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
            return;
        }

        var (success, error) = _settings.AddFolder(path, name);
        if (!success)
        {
            MessageBox.Show(error ?? _loc["Dialog.UnknownError"], _loc["Dialog.Error"]);
            return;
        }
        WeakReferenceMessenger.Default.Send(new FolderAddedMessage(name, path, scanResult.VideoCount, scanResult.CoverPath));
    }

    private async void EnterPlayerPage()
    {
        if (CurrentAnimationCode == "none")
            return;

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

