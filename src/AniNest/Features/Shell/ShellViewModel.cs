using System.Windows.Input;
using System.Linq;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Features.Library;
using AniNest.Features.Library.Services;
using AniNest.Features.Player;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Services;
using AniNest.Features.Player.Settings;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Interop;
using AniNest.Infrastructure.Presentation;

namespace AniNest.Features.Shell;

public partial class ShellViewModel : ObservableObject
{
    private static readonly Logger Log = AppLog.For<ShellViewModel>();
    private readonly ILocalizationService _loc;
    private readonly ILibraryAppService _libraryService;
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly IShellNavigationAppService _shellNavigationAppService;
    private readonly IDialogService _dialogs;
    private readonly IFolderPickerService _folderPicker;
    private readonly MainPageViewModel _mainPage;
    private readonly PlayerViewModel _playerPage;
    private readonly PropertyChangedEventHandler _playerDisplayStatePropertyChangedHandler;
    private bool _isPageTransitionPending;
    private string? _pendingTransitionTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnMainPage))]
    [NotifyPropertyChangedFor(nameof(IsOnPlayerPage))]
    [NotifyPropertyChangedFor(nameof(TitleBarPrimaryActionText))]
    [NotifyPropertyChangedFor(nameof(TitleBarPrimaryActionToolTip))]
    private object? _currentPage;

    public bool IsOnMainPage => CurrentPage is MainPageViewModel;
    public bool IsOnPlayerPage => CurrentPage is PlayerViewModel;
    public string CurrentPlayerTitleBarText => _playerPage.DisplayState.CurrentVideoFileName;
    public string? CurrentPlayerTitleBarToolTip => _playerPage.DisplayState.CurrentVideoPath;
    public string TitleBarPrimaryActionText => IsOnPlayerPage
        ? _loc["KeyBinding.Back"]
        : _loc["App.File"];
    public string TitleBarPrimaryActionToolTip => TitleBarPrimaryActionText;

    partial void OnCurrentPageChanged(object? value)
    {
        OnPropertyChanged(nameof(CurrentPlayerTitleBarText));
        OnPropertyChanged(nameof(CurrentPlayerTitleBarToolTip));
    }

    public event Action? ToggleFullscreenRequested;

    public ILocalizationService Localization => _loc;
    public PlayerInputSettingsViewModel PlayerInputSettings { get; }
    public ShellSettingsPanelViewModel SettingsPanel { get; }
    public ShellThumbnailStatusPanelViewModel ThumbnailStatusPanel { get; }

    public ShellViewModel(
        ILocalizationService loc,
        ILibraryAppService libraryService,
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        IShellNavigationAppService shellNavigationAppService,
        IDialogService dialogs,
        IFolderPickerService folderPicker,
        IApplicationLifecycle applicationLifecycle,
        MainPageViewModel mainPage,
        PlayerViewModel playerPage,
        PlayerInputSettingsViewModel playerInputSettings,
        ShellSettingsPanelViewModel settingsPanel,
        ShellThumbnailStatusPanelViewModel thumbnailStatusPanel)
    {
        _loc = loc;
        _libraryService = libraryService;
        _taskbarAutoHide = taskbarAutoHide;
        _shellNavigationAppService = shellNavigationAppService;
        _dialogs = dialogs;
        _folderPicker = folderPicker;
        _mainPage = mainPage;
        _playerPage = playerPage;
        _playerDisplayStatePropertyChangedHandler = OnPlayerDisplayStatePropertyChanged;
        PlayerInputSettings = playerInputSettings;
        SettingsPanel = settingsPanel;
        ThumbnailStatusPanel = thumbnailStatusPanel;
        _mainPage.FolderSelected += OnMainPageFolderSelected;
        _playerPage.ToggleFullscreenRequested += OnPlayerToggleFullscreenRequested;
        _playerPage.GoBackRequested += OnPlayerGoBackRequested;
        _playerPage.DisplayState.PropertyChanged += _playerDisplayStatePropertyChangedHandler;
        SettingsPanel.LocalizedDisplayTextChanged += OnSettingsPanelLocalizedDisplayTextChanged;
        applicationLifecycle.ExitRequested += (_, _) => _taskbarAutoHide.RestoreIfNeeded();

        Log.Info($"ShellViewModel initialized. CurrentAnimation={SettingsPanel.CurrentAnimationCode}, CurrentLanguage={SettingsPanel.CurrentLanguageCode}");
        CurrentPage = _mainPage;
    }

    public void OnPageTransitionCompleted()
    {
        Log.Info($"OnPageTransitionCompleted. CurrentPage={CurrentPage?.GetType().Name ?? "null"}");
        _isPageTransitionPending = false;
        _pendingTransitionTarget = null;
        _shellNavigationAppService.CompletePlayerPageTransition(CurrentPage is PlayerViewModel);
        Log.Info("OnPageTransitionCompleted finished");
    }

    public void SetPlayerFullscreen(bool value)
        => _playerPage.SetFullscreen(value);

    private void OnMainPageFolderSelected(string path, string name)
    {
        if (!_shellNavigationAppService.CanEnterPlayerPage(_isPageTransitionPending, ReferenceEquals(CurrentPage, _mainPage)))
        {
            Log.Warning(
                $"Ignore folder selection during pending transition: name={name}, path={path}, " +
                $"currentPage={CurrentPage?.GetType().Name ?? "null"}, pendingTarget={_pendingTransitionTarget ?? "null"}");
            return;
        }

        Log.Info($"Folder selected: {name} | {path}");
        _isPageTransitionPending = true;
        _pendingTransitionTarget = nameof(PlayerViewModel);
        CurrentPage = _playerPage;
        _ = _shellNavigationAppService.BeginEnterPlayerPageAsync(SettingsPanel.CurrentAnimationCode, path, name);
    }

    private void OnPlayerToggleFullscreenRequested()
        => ToggleFullscreenRequested?.Invoke();

    private void OnPlayerDisplayStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerDisplayStateViewModel.CurrentVideoPath) ||
            e.PropertyName == nameof(PlayerDisplayStateViewModel.CurrentVideoFileName))
        {
            OnPropertyChanged(nameof(CurrentPlayerTitleBarText));
            OnPropertyChanged(nameof(CurrentPlayerTitleBarToolTip));
        }
    }

    private void OnPlayerGoBackRequested()
    {
        if (!_shellNavigationAppService.CanLeavePlayerPage(_isPageTransitionPending, ReferenceEquals(CurrentPage, _playerPage)))
        {
            Log.Warning(
                $"Ignore player go-back during pending transition: currentPage={CurrentPage?.GetType().Name ?? "null"}, " +
                $"pendingTarget={_pendingTransitionTarget ?? "null"}");
            return;
        }

        Log.Info("Player go-back requested");
        _isPageTransitionPending = true;
        _pendingTransitionTarget = nameof(MainPageViewModel);
        _ = _shellNavigationAppService.BeginLeavePlayerPageAsync();
        CurrentPage = _mainPage;
    }

    [RelayCommand]
    private void GoBackFromPlayerTitleBar()
    {
        OnPlayerGoBackRequested();
    }

    private void OnSettingsPanelLocalizedDisplayTextChanged()
    {
        OnPropertyChanged(nameof(TitleBarPrimaryActionText));
        OnPropertyChanged(nameof(TitleBarPrimaryActionToolTip));
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        string? path = _folderPicker.PickFolder(_loc["Dialog.AddFolder"]);
        if (string.IsNullOrWhiteSpace(path))
            return;

        var result = await _libraryService.AddFolderAsync(path);
        if (!result.Success)
        {
            switch (result.Failure)
            {
                case AddFolderFailure.Duplicate:
                    _dialogs.ShowInfo(_loc["Dialog.FolderAlreadyAdded"], _loc["Dialog.Info"]);
                    break;
                case AddFolderFailure.NoVideos:
                    _dialogs.ShowInfo(_loc["Dialog.NoVideosInFolder"], _loc["Dialog.Info"]);
                    break;
                default:
                    _dialogs.ShowError(result.ErrorMessage ?? _loc["Dialog.UnknownError"], _loc["Dialog.Error"]);
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
        string? rootPath = _folderPicker.PickFolder(_loc["Dialog.AddFolderBatch"]);
        if (string.IsNullOrWhiteSpace(rootPath))
            return;

        Log.Info($"AddFolderBatch: user selected {rootPath}");

        var result = await _libraryService.AddFolderBatchAsync(rootPath);
        if (result.AddedFolders.Count == 0 && result.SkippedCount == 0)
        {
            Log.Info("AddFolderBatch: no video folders found");
            _dialogs.ShowInfo(_loc["Dialog.NoVideoFoldersFound"], _loc["Dialog.Info"]);
            return;
        }

        foreach (var folder in result.AddedFolders)
            _mainPage.AddFolderItem(folder.Name, folder.Path, folder.VideoCount, folder.CoverPath);

        Log.Info($"AddFolderBatch: done, added {result.AddedFolders.Count} items, skipped {result.SkippedCount}");
        string msg = string.Format(_loc["Dialog.BatchResult"], result.AddedFolders.Count, result.SkippedCount);
        _dialogs.ShowInfo(msg, _loc["Dialog.Info"]);
    }

    public bool TryCaptureSettingsKey(PlayerInputKeyEvent inputEvent) => PlayerInputSettings.TryCaptureKey(inputEvent);
    public bool TryCaptureSettingsMouseDown(PlayerInputMouseButtonEvent inputEvent) => PlayerInputSettings.TryCaptureMouseDown(inputEvent);
    public bool TryCaptureSettingsMouseWheel(PlayerInputMouseWheelEvent inputEvent) => PlayerInputSettings.TryCaptureMouseWheel(inputEvent);

    public bool TryHandlePlayerKeyDown(PlayerInputKeyEvent inputEvent)
    {
        if (CurrentPage is PlayerViewModel player)
            return player.InputService.TryHandleKeyDown(player, inputEvent);

        return false;
    }

}



