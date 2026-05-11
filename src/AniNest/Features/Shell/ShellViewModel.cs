using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
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
using AniNest.Infrastructure.Thumbnails;

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
    private readonly IShellSettingsAppService _shellSettingsAppService;
    private readonly IShellThumbnailPerformanceAppService _thumbnailPerformanceAppService;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly MainPageViewModel _mainPage;
    private readonly PlayerViewModel _playerPage;
    private string? _lastThumbnailGenerationStatusLog;
    private bool _isPageTransitionPending;
    private string? _pendingTransitionTarget;
    private readonly SelectableOptionItem _languageChineseOption = new("zh-CN", "简体中文");
    private readonly SelectableOptionItem _languageEnglishOption = new("en-US", "English");
    private readonly SelectableOptionItem _fullscreenAnimationNoneOption = new("none", string.Empty);
    private readonly SelectableOptionItem _fullscreenAnimationContinuousOption = new("continuous", string.Empty);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnMainPage))]
    [NotifyPropertyChangedFor(nameof(IsOnPlayerPage))]
    private object? _currentPage;

    public bool IsOnMainPage => CurrentPage is MainPageViewModel;
    public bool IsOnPlayerPage => CurrentPage is PlayerViewModel;
    public string CurrentPlayerTitleBarText => _playerPage.CurrentVideoFileName;
    public string? CurrentPlayerTitleBarToolTip => _playerPage.CurrentVideoPath;

    [ObservableProperty]
    private bool _isFilePopupOpen;

    [ObservableProperty]
    private bool _isSettingsPopupOpen;

    [ObservableProperty]
    private bool _isLanguageSubmenuOpen;

    [ObservableProperty]
    private bool _isFullscreenAnimationSubmenuOpen;

    [ObservableProperty]
    private bool _isThumbnailSettingsSubmenuOpen;

    [ObservableProperty]
    private bool _isThumbnailPerformanceSubmenuOpen;

    [ObservableProperty]
    private bool _isThumbnailAccelerationSubmenuOpen;

    [ObservableProperty]
    private bool _isPlayerInputSubmenuOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectThumbnailPerformanceMode))]
    private bool _isApplyingThumbnailPerformanceMode;

    [ObservableProperty]
    private string _thumbnailDetectedHardwareSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailCurrentDecoderSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailFallbackChainSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailGenerationStatusText = string.Empty;

    [ObservableProperty]
    private string _thumbnailGenerationStatusCode = "idle";

    [ObservableProperty]
    private string _thumbnailGenerationStatusColor = "#8E8E93";

    [ObservableProperty]
    private string _thumbnailGenerationSummaryText = string.Empty;

    [ObservableProperty]
    private string _thumbnailBackgroundTasksHeaderText = string.Empty;

    [ObservableProperty]
    private double _thumbnailGenerationProgressPercent;

    public ObservableCollection<ThumbnailBackgroundTaskItemViewModel> ThumbnailBackgroundTasks { get; } = new();
    public ObservableCollection<SelectableOptionItem> LanguageOptions { get; } = new();
    public ObservableCollection<SelectableOptionItem> FullscreenAnimationOptions { get; } = new();

    public event Action? ToggleFullscreenRequested;

    public ILocalizationService Localization => _loc;
    public IReadOnlyList<LanguageInfo> AvailableLanguages => _loc.AvailableLanguages;
    public PlayerInputSettingsViewModel PlayerInputSettings { get; }
    public string CurrentLanguageCode => _preferencesService.CurrentLanguageCode;
    public string CurrentAnimationCode => _preferencesService.CurrentFullscreenAnimationCode;
    public string CurrentThumbnailPerformanceModeCode => _preferencesService.CurrentThumbnailPerformanceModeCode;
    public string CurrentThumbnailAccelerationModeCode => _preferencesService.CurrentThumbnailAccelerationModeCode;
    public string ThumbnailPerformanceSummary => IsThumbnailPerformancePaused
        ? _loc["Settings.ThumbnailPerformance.Paused"]
        : _loc[$"Settings.ThumbnailPerformance.{CapitalizeCode(CurrentThumbnailPerformanceModeCode)}"];
    public string ThumbnailAccelerationSummary => _loc[$"Settings.ThumbnailAcceleration.{CapitalizeCode(CurrentThumbnailAccelerationModeCode)}"];
    public int LanguageSelectedIndex => string.Equals(CurrentLanguageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    public int FullscreenAnimationSelectedIndex => string.Equals(CurrentAnimationCode, "continuous", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    public int ThumbnailPerformanceSelectedIndex => CurrentThumbnailPerformanceModeCode switch
        {
            "paused" => 0,
            "quiet" => 1,
            "fast" => 3,
            _ => 2
        };
    public int ThumbnailAccelerationSelectedIndex => string.Equals(CurrentThumbnailAccelerationModeCode, "compatible", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    public bool IsLanguageChineseSelected => LanguageSelectedIndex == 0;
    public bool IsLanguageEnglishSelected => LanguageSelectedIndex == 1;
    public bool IsFullscreenAnimationNoneSelected => FullscreenAnimationSelectedIndex == 0;
    public bool IsFullscreenAnimationContinuousSelected => FullscreenAnimationSelectedIndex == 1;
    public bool IsThumbnailPerformancePausedSelected => ThumbnailPerformanceSelectedIndex == 0;
    public bool IsThumbnailPerformanceQuietSelected => ThumbnailPerformanceSelectedIndex == 1;
    public bool IsThumbnailPerformanceBalancedSelected => ThumbnailPerformanceSelectedIndex == 2;
    public bool IsThumbnailPerformanceFastSelected => ThumbnailPerformanceSelectedIndex == 3;
    public bool IsThumbnailPerformancePaused => string.Equals(CurrentThumbnailPerformanceModeCode, "paused", StringComparison.OrdinalIgnoreCase);
    public bool CanSelectThumbnailPerformanceMode => !IsApplyingThumbnailPerformanceMode;
    public bool IsThumbnailAccelerationAutoSelected => ThumbnailAccelerationSelectedIndex == 0;
    public bool IsThumbnailAccelerationCompatibleSelected => ThumbnailAccelerationSelectedIndex == 1;

    public ShellViewModel(
        ILocalizationService loc,
        ILibraryAppService libraryService,
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        IPlayerAppService playerAppService,
        IShellPreferencesService preferencesService,
        IShellSettingsAppService shellSettingsAppService,
        IShellThumbnailPerformanceAppService thumbnailPerformanceAppService,
        IThumbnailGenerator thumbnailGenerator,
        MainPageViewModel mainPage,
        PlayerViewModel playerPage,
        PlayerInputSettingsViewModel playerInputSettings)
    {
        _loc = loc;
        _libraryService = libraryService;
        _taskbarAutoHide = taskbarAutoHide;
        _playerAppService = playerAppService;
        _preferencesService = preferencesService;
        _shellSettingsAppService = shellSettingsAppService;
        _thumbnailPerformanceAppService = thumbnailPerformanceAppService;
        _thumbnailGenerator = thumbnailGenerator;
        _mainPage = mainPage;
        _playerPage = playerPage;
        PlayerInputSettings = playerInputSettings;
        _mainPage.FolderSelected += OnMainPageFolderSelected;
        _playerPage.ToggleFullscreenRequested += OnPlayerToggleFullscreenRequested;
        _playerPage.GoBackRequested += OnPlayerGoBackRequested;
        _playerPage.PropertyChanged += OnPlayerPagePropertyChanged;
        _thumbnailGenerator.StatusChanged += OnThumbnailGeneratorStatusChanged;

        InitializeSelectableOptions();
        Application.Current.Exit += (_, _) => _taskbarAutoHide.RestoreIfNeeded();
        RefreshThumbnailSettingsStatus();
        RefreshSelectableOptionContent();
        RefreshSelectableOptionSelectionState();

        Log.Info($"ShellViewModel initialized. CurrentAnimation={CurrentAnimationCode}, CurrentLanguage={CurrentLanguageCode}");
        CurrentPage = _mainPage;
    }

    public void OnPageTransitionCompleted()
    {
        var sw = Stopwatch.StartNew();
        Log.Info($"OnPageTransitionCompleted. CurrentPage={CurrentPage?.GetType().Name ?? "null"}");
        _isPageTransitionPending = false;
        _pendingTransitionTarget = null;

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

    partial void OnCurrentPageChanged(object? value)
    {
        OnPropertyChanged(nameof(CurrentPlayerTitleBarText));
        OnPropertyChanged(nameof(CurrentPlayerTitleBarToolTip));
    }

    private void OnMainPageFolderSelected(string path, string name)
    {
        if (_isPageTransitionPending || !ReferenceEquals(CurrentPage, _mainPage))
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
        _ = EnterPlayerPageAsync(path, name);
    }

    private void OnPlayerToggleFullscreenRequested()
        => ToggleFullscreenRequested?.Invoke();

    private void OnPlayerPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.CurrentVideoPath) ||
            e.PropertyName == nameof(PlayerViewModel.CurrentVideoFileName))
        {
            OnPropertyChanged(nameof(CurrentPlayerTitleBarText));
            OnPropertyChanged(nameof(CurrentPlayerTitleBarToolTip));
        }
    }

    private void OnPlayerGoBackRequested()
    {
        if (_isPageTransitionPending || !ReferenceEquals(CurrentPage, _playerPage))
        {
            Log.Warning(
                $"Ignore player go-back during pending transition: currentPage={CurrentPage?.GetType().Name ?? "null"}, " +
                $"pendingTarget={_pendingTransitionTarget ?? "null"}");
            return;
        }

        Log.Info("Player go-back requested");
        _isPageTransitionPending = true;
        _pendingTransitionTarget = nameof(MainPageViewModel);
        _ = LeavePlayerPageAsync();
        CurrentPage = _mainPage;
    }

    [RelayCommand]
    private void GoBackFromPlayerTitleBar()
    {
        OnPlayerGoBackRequested();
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
            IsThumbnailSettingsSubmenuOpen = false;
            IsThumbnailPerformanceSubmenuOpen = false;
            IsThumbnailAccelerationSubmenuOpen = false;
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
    private void SwitchLanguage(string code)
    {
        _shellSettingsAppService.SetLanguage(code);
        NotifyLanguageSettingsChanged();
        RefreshThumbnailSettingsStatus();
        RefreshSelectableOptionContent();
        RefreshSelectableOptionSelectionState();
    }

    [RelayCommand]
    private void SelectFullscreenAnimation(string code)
    {
        _shellSettingsAppService.SetFullscreenAnimation(code);
        NotifyFullscreenAnimationSettingsChanged();
        RefreshSelectableOptionSelectionState();
    }

    [RelayCommand(CanExecute = nameof(CanSelectThumbnailPerformanceMode))]
    private async Task SelectThumbnailPerformanceModeAsync(string code)
    {
        if (IsApplyingThumbnailPerformanceMode)
            return;

        try
        {
            IsApplyingThumbnailPerformanceMode = true;
            SelectThumbnailPerformanceModeCommand.NotifyCanExecuteChanged();
            await _thumbnailPerformanceAppService.TrySetPerformanceModeAsync(code);
        }
        finally
        {
            NotifyThumbnailPerformanceSettingsChanged();
            RefreshThumbnailSettingsStatus();
            IsApplyingThumbnailPerformanceMode = false;
            SelectThumbnailPerformanceModeCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void SelectThumbnailAccelerationMode(string code)
    {
        _shellSettingsAppService.SetThumbnailAccelerationMode(code);
        NotifyThumbnailAccelerationSettingsChanged();
        RefreshThumbnailSettingsStatus();
    }

    private void RefreshThumbnailSettingsStatus()
    {
        var status = _preferencesService.CurrentThumbnailDecodeStatus;
        ThumbnailDetectedHardwareSummary = BuildHardwareSummary(status);
        ThumbnailCurrentDecoderSummary = BuildCurrentDecoderSummary(status);
        ThumbnailFallbackChainSummary = string.Join(" -> ", status.StrategyChain.Select(FormatStrategyName));
        RefreshThumbnailGenerationStatus();
    }

    private void InitializeSelectableOptions()
    {
        LanguageOptions.Clear();
        LanguageOptions.Add(_languageChineseOption);
        LanguageOptions.Add(_languageEnglishOption);

        FullscreenAnimationOptions.Clear();
        FullscreenAnimationOptions.Add(_fullscreenAnimationNoneOption);
        FullscreenAnimationOptions.Add(_fullscreenAnimationContinuousOption);
    }

    private void RefreshSelectableOptionContent()
    {
        _fullscreenAnimationNoneOption.DisplayName = _loc["Settings.FullscreenAnimation.NoAnimation"];
        _fullscreenAnimationContinuousOption.DisplayName = _loc["Settings.FullscreenAnimation.ContinuousAnimation"];
    }

    private void RefreshSelectableOptionSelectionState()
    {
        foreach (var option in LanguageOptions)
            option.IsSelected = string.Equals(option.Code, CurrentLanguageCode, StringComparison.OrdinalIgnoreCase);

        foreach (var option in FullscreenAnimationOptions)
            option.IsSelected = string.Equals(option.Code, CurrentAnimationCode, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshThumbnailGenerationStatus()
    {
        var snapshot = _thumbnailGenerator.GetStatusSnapshot();

        ThumbnailGenerationStatusCode = snapshot.IsPaused
            ? "paused"
            : snapshot.ActiveWorkers > 0
                ? "generating"
                : snapshot.PendingCount > 0
                    ? "waiting"
                : snapshot.ReadyCount >= snapshot.TotalCount && snapshot.TotalCount > 0
                    ? "complete"
                    : "idle";

        ThumbnailGenerationStatusText = _loc[$"Settings.ThumbnailGeneration.Status.{CapitalizeCode(ThumbnailGenerationStatusCode)}"];
        ThumbnailGenerationStatusColor = ThumbnailGenerationStatusCode switch
        {
            "paused" => "#C62828",
            "generating" => "#007AFF",
            "waiting" => "#F39C12",
            "complete" => "#2E7D32",
            _ => "#8E8E93"
        };

        ThumbnailGenerationProgressPercent = snapshot.TotalCount > 0
            ? (double)snapshot.ReadyCount / snapshot.TotalCount * 100
            : 0;
        ThumbnailGenerationSummaryText = string.Format(_loc["Settings.ThumbnailGeneration.Summary"], snapshot.ReadyCount, snapshot.TotalCount);
        ThumbnailBackgroundTasksHeaderText = _loc["TitleBar.ThumbnailBackgroundTasks"];
        ThumbnailBackgroundTasks.Clear();
        foreach (var task in snapshot.ActiveTasks.Take(2))
            ThumbnailBackgroundTasks.Add(new ThumbnailBackgroundTaskItemViewModel(task, _loc));

        string statusLog =
            $"code={ThumbnailGenerationStatusCode}, ready={snapshot.ReadyCount}, total={snapshot.TotalCount}, " +
            $"active={snapshot.ActiveWorkers}, pending={snapshot.PendingCount}, foregroundPending={snapshot.ForegroundPendingCount}, " +
            $"target={snapshot.CurrentTargetIntent ?? "none"}:{snapshot.CurrentTargetName ?? "none"}, " +
            $"paused={snapshot.IsPaused}, playerActive={snapshot.IsPlayerActive}";
        if (!string.Equals(_lastThumbnailGenerationStatusLog, statusLog, StringComparison.Ordinal))
        {
            _lastThumbnailGenerationStatusLog = statusLog;
            Log.Debug($"Thumbnail generation UI status updated: {statusLog}");
        }
    }

    private void OnThumbnailGeneratorStatusChanged()
    {
        Application.Current.Dispatcher.BeginInvoke((Action)RefreshThumbnailSettingsStatus);
    }

    private void NotifyLanguageSettingsChanged()
    {
        OnPropertyChanged(nameof(CurrentLanguageCode));
        OnPropertyChanged(nameof(LanguageSelectedIndex));
        OnPropertyChanged(nameof(IsLanguageChineseSelected));
        OnPropertyChanged(nameof(IsLanguageEnglishSelected));
    }

    private void NotifyFullscreenAnimationSettingsChanged()
    {
        OnPropertyChanged(nameof(CurrentAnimationCode));
        OnPropertyChanged(nameof(FullscreenAnimationSelectedIndex));
        OnPropertyChanged(nameof(IsFullscreenAnimationNoneSelected));
        OnPropertyChanged(nameof(IsFullscreenAnimationContinuousSelected));
    }

    private void NotifyThumbnailPerformanceSettingsChanged()
    {
        OnPropertyChanged(nameof(CurrentThumbnailPerformanceModeCode));
        OnPropertyChanged(nameof(ThumbnailPerformanceSelectedIndex));
        OnPropertyChanged(nameof(ThumbnailPerformanceSummary));
        OnPropertyChanged(nameof(IsThumbnailPerformancePausedSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformanceQuietSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformanceBalancedSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformanceFastSelected));
        OnPropertyChanged(nameof(IsThumbnailPerformancePaused));
    }

    private void NotifyThumbnailAccelerationSettingsChanged()
    {
        OnPropertyChanged(nameof(CurrentThumbnailAccelerationModeCode));
        OnPropertyChanged(nameof(ThumbnailAccelerationSelectedIndex));
        OnPropertyChanged(nameof(ThumbnailAccelerationSummary));
        OnPropertyChanged(nameof(IsThumbnailAccelerationAutoSelected));
        OnPropertyChanged(nameof(IsThumbnailAccelerationCompatibleSelected));
    }

    private string BuildHardwareSummary(ThumbnailDecodeStatusSnapshot status)
    {
        List<string> items = [];
        if (status.SupportsCuda)
            items.Add("CUDA");
        if (status.SupportsQsv)
            items.Add("QSV");
        if (status.SupportsD3D11VA)
            items.Add("D3D11VA");

        return items.Count > 0 ? string.Join(", ", items) : _loc["Settings.ThumbnailAcceleration.Hardware.None"];
    }

    private string BuildCurrentDecoderSummary(ThumbnailDecodeStatusSnapshot status)
    {
        if (status.PreferredStrategy is not null)
            return FormatStrategyName(status.PreferredStrategy.Value);

        if (status.StrategyChain.Count > 0)
            return FormatStrategyName(status.StrategyChain[0]);

        return FormatStrategyName(ThumbnailDecodeStrategy.Software);
    }

    private static string CapitalizeCode(string code)
        => string.IsNullOrWhiteSpace(code) ? string.Empty : char.ToUpperInvariant(code[0]) + code[1..];

    private static string FormatStrategyName(ThumbnailDecodeStrategy strategy)
        => strategy switch
        {
            ThumbnailDecodeStrategy.NvidiaCuda => "NVIDIA CUDA",
            ThumbnailDecodeStrategy.IntelQsv => "Intel QSV",
            ThumbnailDecodeStrategy.D3D11VA => "D3D11VA",
            ThumbnailDecodeStrategy.AutoHardware => "Auto Hardware",
            _ => "Software"
        };

    public sealed class ThumbnailBackgroundTaskItemViewModel
    {
        public ThumbnailBackgroundTaskItemViewModel(ThumbnailActiveTaskSnapshot snapshot, ILocalizationService loc)
        {
            FileName = snapshot.VideoName;
            IntentText = FormatIntent(snapshot.Intent, loc);
            StatusCode = FormatStatusCode(snapshot);
            ProgressPercent = snapshot.ProgressPercent;
            IsSuspended = snapshot.IsSuspended;
            StatusText = snapshot.IsSuspended
                ? loc["Settings.ThumbnailGeneration.Status.Paused"]
                : loc[$"Settings.ThumbnailGeneration.Status.{snapshot.State}"];
        }

        public string FileName { get; }
        public string IntentText { get; }
        public string StatusText { get; }
        public string StatusCode { get; }
        public int ProgressPercent { get; }
        public bool IsSuspended { get; }

        private static string FormatIntent(ThumbnailWorkIntent intent, ILocalizationService loc)
            => intent switch
            {
                ThumbnailWorkIntent.ManualSingle => loc["Settings.ThumbnailGeneration.Intent.Current"],
                ThumbnailWorkIntent.PlaybackCurrent => loc["Settings.ThumbnailGeneration.Intent.Now"],
                ThumbnailWorkIntent.PlaybackNearby => loc["Settings.ThumbnailGeneration.Intent.Nearby"],
                ThumbnailWorkIntent.ManualCollection => loc["Settings.ThumbnailGeneration.Intent.Manual"],
                ThumbnailWorkIntent.FocusedCollection => loc["Settings.ThumbnailGeneration.Intent.Focused"],
                _ => loc["Settings.ThumbnailGeneration.Intent.Background"]
            };

        private static string FormatStatusCode(ThumbnailActiveTaskSnapshot snapshot)
        {
            if (snapshot.IsSuspended)
                return "paused";

            return snapshot.State switch
            {
                ThumbnailState.Pending => "waiting",
                ThumbnailState.PausedGenerating => "paused",
                ThumbnailState.Ready => "complete",
                ThumbnailState.Failed => "waiting",
                _ => "generating"
            };
        }
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

    private async Task EnterPlayerPageAsync(string path, string name)
    {
        try
        {
            await _playerAppService.EnterPlayerAsync(CurrentAnimationCode, path, name);
            Log.Info($"EnterPlayerAsync finished: name={name}, path={path}");
        }
        catch (Exception ex)
        {
            Log.Error($"EnterPlayerAsync failed: name={name}, path={path}", ex);
        }
    }

    private async Task LeavePlayerPageAsync()
    {
        try
        {
            await _playerAppService.BeginLeavePlayerAsync();
            Log.Info("BeginLeavePlayerAsync finished");
        }
        catch (Exception ex)
        {
            Log.Error("BeginLeavePlayerAsync failed", ex);
        }
    }

}



