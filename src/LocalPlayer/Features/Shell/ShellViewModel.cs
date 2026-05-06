using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Features.Library;
using LocalPlayer.Features.Player;
using LocalPlayer.Features.Player.Settings;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;

namespace LocalPlayer.Features.Shell;

public partial class ShellViewModel : ObservableObject
{
    private static readonly Logger Log = AppLog.For<ShellViewModel>();
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly IPlayerViewCoordinator _playerCoordinator;
    private readonly MainPageViewModel _mainPage;
    private readonly PlayerViewModel _playerPage;

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
    private bool _isPlayerInputSubmenuOpen;

    [ObservableProperty]
    private string _currentLanguageCode = "zh-CN";

    [ObservableProperty]
    private string _currentAnimationCode = "continuous";

    public event Action? ToggleFullscreenRequested;

    public ILocalizationService Localization => _loc;
    public IReadOnlyList<LanguageInfo> AvailableLanguages => _loc.AvailableLanguages;
    public PlayerInputSettingsViewModel PlayerInputSettings { get; }

    public ShellViewModel(
        ISettingsService settings,
        ILocalizationService loc,
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        IPlayerViewCoordinator playerCoordinator,
        MainPageViewModel mainPage,
        PlayerViewModel playerPage,
        PlayerInputSettingsViewModel playerInputSettings)
    {
        _settings = settings;
        _loc = loc;
        _taskbarAutoHide = taskbarAutoHide;
        _playerCoordinator = playerCoordinator;
        _currentLanguageCode = _loc.CurrentLanguage;
        _currentAnimationCode = _settings.Load().FullscreenAnimation;
        _mainPage = mainPage;
        _playerPage = playerPage;
        PlayerInputSettings = playerInputSettings;
        _mainPage.FolderSelected += OnMainPageFolderSelected;
        _playerPage.ToggleFullscreenRequested += OnPlayerToggleFullscreenRequested;
        _playerPage.GoBackRequested += OnPlayerGoBackRequested;

        Application.Current.Exit += (_, _) => _taskbarAutoHide.RestoreIfNeeded();

        Log.Info($"ShellViewModel initialized. CurrentAnimation={_currentAnimationCode}, CurrentLanguage={_currentLanguageCode}");
        CurrentPage = _mainPage;
    }

    public void OnPageTransitionCompleted()
    {
        Log.Info($"OnPageTransitionCompleted. CurrentPage={CurrentPage?.GetType().Name ?? "null"}");
        if (CurrentPage is PlayerViewModel)
        {
            Log.Info("Player page transition completed, requesting LoadFolderDataAsync");
            _ = _playerCoordinator.LoadFolderDataAsync();
        }
    }

    public void SetPlayerFullscreen(bool value)
        => _playerPage.SetFullscreen(value);

    private void OnMainPageFolderSelected(string path, string name)
    {
        Log.Info($"Folder selected: {name} | {path}");
        _ = _playerCoordinator.EnterPlayerPageAsync(CurrentAnimationCode);
        CurrentPage = _playerPage;
        _playerCoordinator.LoadFolderSkeleton(path, name);
        _ = _playerCoordinator.LoadFolderDataAsync();
    }

    private void OnPlayerToggleFullscreenRequested()
        => ToggleFullscreenRequested?.Invoke();

    private void OnPlayerGoBackRequested()
    {
        _ = _taskbarAutoHide.LeavePlayerPageAsync();
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
            IsPlayerInputSubmenuOpen = false;
            PlayerInputSettings.CancelCapture();
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
    private void TogglePlayerInputSubmenu()
    {
        IsPlayerInputSubmenuOpen = !IsPlayerInputSubmenuOpen;
        if (IsPlayerInputSubmenuOpen)
            PlayerInputSettings.RefreshFromService();
        else
            PlayerInputSettings.CancelCapture();
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
        _mainPage.AddFolderItem(name, path, scanResult.VideoCount, scanResult.CoverPath);
    }

    public bool TryCaptureSettingsKey(KeyEventArgs args) => PlayerInputSettings.TryCaptureKey(args);
    public bool TryCaptureSettingsMouseDown(MouseButtonEventArgs args) => PlayerInputSettings.TryCaptureMouseDown(args);
    public bool TryCaptureSettingsMouseWheel(MouseWheelEventArgs args) => PlayerInputSettings.TryCaptureMouseWheel(args);

    public void TryHandlePlayerKeyDown(KeyEventArgs args)
    {
        if (CurrentPage is PlayerViewModel player)
            player.InputService.TryHandlePreviewKeyDown(player, args);
    }

}



