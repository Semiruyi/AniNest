using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Library;
using AniNest.Features.Library.Services;
using AniNest.Features.Player;
using AniNest.Features.Player.Services;
using AniNest.Features.Player.Settings;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Interop;

namespace AniNest.Features.Shell;

public partial class ShellViewModel : ObservableObject
{
    private static readonly Logger Log = AppLog.For<ShellViewModel>();
    private const double FrameBudgetMs160Hz = 1000.0 / 160.0;
    private readonly ILocalizationService _loc;
    private readonly ILibraryAppService _libraryService;
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly IPlayerAppService _playerAppService;
    private readonly IShellPreferencesService _preferencesService;
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
        ILocalizationService loc,
        ILibraryAppService libraryService,
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        IPlayerAppService playerAppService,
        IShellPreferencesService preferencesService,
        MainPageViewModel mainPage,
        PlayerViewModel playerPage,
        PlayerInputSettingsViewModel playerInputSettings)
    {
        _loc = loc;
        _libraryService = libraryService;
        _taskbarAutoHide = taskbarAutoHide;
        _playerAppService = playerAppService;
        _preferencesService = preferencesService;
        _currentLanguageCode = _loc.CurrentLanguage;
        _currentAnimationCode = _preferencesService.CurrentFullscreenAnimationCode;
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
        var sw = Stopwatch.StartNew();
        Log.Info($"OnPageTransitionCompleted. CurrentPage={CurrentPage?.GetType().Name ?? "null"}");

        if (CurrentPage is PlayerViewModel)
        {
            _playerAppService.OnPlayerPageTransitionCompleted();
        }
        else if (CurrentPage is MainPageViewModel)
        {
            _playerAppService.CompleteLeavePlayerTransition();
        }

        sw.Stop();
        var overBudget = sw.Elapsed.TotalMilliseconds > FrameBudgetMs160Hz;
        Log.Info(
            $"OnPageTransitionCompleted finished in {sw.Elapsed.TotalMilliseconds:F3}ms " +
            $"(budget {FrameBudgetMs160Hz:F2}ms @160Hz, overBudget={overBudget})");
    }

    public void SetPlayerFullscreen(bool value)
        => _playerPage.SetFullscreen(value);

    private void OnMainPageFolderSelected(string path, string name)
    {
        Log.Info($"Folder selected: {name} | {path}");
        CurrentPage = _playerPage;
        _ = _playerAppService.EnterPlayerAsync(CurrentAnimationCode, path, name);
    }

    private void OnPlayerToggleFullscreenRequested()
        => ToggleFullscreenRequested?.Invoke();

    private void OnPlayerGoBackRequested()
    {
        _ = _playerAppService.BeginLeavePlayerAsync();
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
        _preferencesService.SetLanguage(code);
        CurrentLanguageCode = _preferencesService.CurrentLanguageCode;
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
        _preferencesService.SetFullscreenAnimation(code);
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        IsFilePopupOpen = false;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = _loc["Dialog.AddFolder"]
        };

        if (dialog.ShowDialog() != true) return;

        string path = dialog.FolderName;
        var result = await _libraryService.AddFolderAsync(path);
        if (!result.Success)
        {
            switch (result.Failure)
            {
                case AddFolderFailure.Duplicate:
                    MessageBox.Show(_loc["Dialog.FolderAlreadyAdded"], _loc["Dialog.Info"]);
                    break;
                case AddFolderFailure.NoVideos:
                    MessageBox.Show(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
                    break;
                default:
                    MessageBox.Show(result.ErrorMessage ?? _loc["Dialog.UnknownError"], _loc["Dialog.Error"]);
                    break;
            }
            return;
        }

        var folder = result.Folder!;
        _mainPage.AddFolderItem(folder.Name, folder.Path, folder.VideoCount, folder.CoverPath);
    }

    [RelayCommand]
    private async Task AddFolderBatch()
    {
        IsFilePopupOpen = false;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = _loc["Dialog.AddFolderBatch"]
        };

        if (dialog.ShowDialog() != true) return;

        string rootPath = dialog.FolderName;
        Log.Info($"AddFolderBatch: user selected {rootPath}");

        var result = await _libraryService.AddFolderBatchAsync(rootPath);
        if (result.AddedFolders.Count == 0 && result.SkippedCount == 0)
        {
            Log.Info("AddFolderBatch: no video folders found");
            MessageBox.Show(_loc["Dialog.NoVideoFoldersFound"], _loc["Dialog.Info"]);
            return;
        }

        foreach (var folder in result.AddedFolders)
            _mainPage.AddFolderItem(folder.Name, folder.Path, folder.VideoCount, folder.CoverPath);

        Log.Info($"AddFolderBatch: done, added {result.AddedFolders.Count} items, skipped {result.SkippedCount}");
        string msg = string.Format(_loc["Dialog.BatchResult"], result.AddedFolders.Count, result.SkippedCount);
        MessageBox.Show(msg, _loc["Dialog.Info"]);
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



