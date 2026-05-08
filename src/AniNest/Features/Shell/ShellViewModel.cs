using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IThumbnailDecodeStrategyService _thumbnailDecodeStrategyService;
    private readonly MainPageViewModel _mainPage;
    private readonly PlayerViewModel _playerPage;
    private string? _lastThumbnailGenerationStatusLog;

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
    private bool _isThumbnailSettingsSubmenuOpen;

    [ObservableProperty]
    private bool _isThumbnailPerformanceSubmenuOpen;

    [ObservableProperty]
    private bool _isThumbnailAccelerationSubmenuOpen;

    [ObservableProperty]
    private bool _isPlayerInputSubmenuOpen;

    [ObservableProperty]
    private string _currentLanguageCode = "zh-CN";

    [ObservableProperty]
    private string _currentAnimationCode = "continuous";

    [ObservableProperty]
    private string _currentThumbnailPerformanceModeCode = "balanced";

    [ObservableProperty]
    private string _currentThumbnailAccelerationModeCode = "auto";

    [ObservableProperty]
    private string _thumbnailPerformanceSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailAccelerationSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailDetectedHardwareSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailCurrentDecoderSummary = string.Empty;

    [ObservableProperty]
    private string _thumbnailFallbackChainSummary = string.Empty;

    [ObservableProperty]
    private bool _isThumbnailGenerationPaused;

    [ObservableProperty]
    private string _thumbnailGenerationToggleText = string.Empty;

    [ObservableProperty]
    private string _thumbnailGenerationStatusText = string.Empty;

    [ObservableProperty]
    private string _thumbnailGenerationStatusCode = "idle";

    [ObservableProperty]
    private string _thumbnailGenerationStatusColor = "#8E8E93";

    [ObservableProperty]
    private string _thumbnailGenerationCountText = string.Empty;

    [ObservableProperty]
    private string _thumbnailGenerationDetailText = string.Empty;

    [ObservableProperty]
    private double _thumbnailGenerationProgressPercent;

    [ObservableProperty]
    private string _thumbnailGenerationTooltipText = string.Empty;

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
        IThumbnailGenerator thumbnailGenerator,
        IThumbnailDecodeStrategyService thumbnailDecodeStrategyService,
        MainPageViewModel mainPage,
        PlayerViewModel playerPage,
        PlayerInputSettingsViewModel playerInputSettings)
    {
        _loc = loc;
        _libraryService = libraryService;
        _taskbarAutoHide = taskbarAutoHide;
        _playerAppService = playerAppService;
        _preferencesService = preferencesService;
        _thumbnailGenerator = thumbnailGenerator;
        _thumbnailDecodeStrategyService = thumbnailDecodeStrategyService;
        _currentLanguageCode = _loc.CurrentLanguage;
        _currentAnimationCode = _preferencesService.CurrentFullscreenAnimationCode;
        _currentThumbnailPerformanceModeCode = _preferencesService.CurrentThumbnailPerformanceModeCode;
        _currentThumbnailAccelerationModeCode = _preferencesService.CurrentThumbnailAccelerationModeCode;
        _isThumbnailGenerationPaused = _preferencesService.IsThumbnailGenerationPaused;
        _mainPage = mainPage;
        _playerPage = playerPage;
        PlayerInputSettings = playerInputSettings;
        _mainPage.FolderSelected += OnMainPageFolderSelected;
        _playerPage.ToggleFullscreenRequested += OnPlayerToggleFullscreenRequested;
        _playerPage.GoBackRequested += OnPlayerGoBackRequested;
        _thumbnailGenerator.StatusChanged += OnThumbnailGeneratorStatusChanged;

        Application.Current.Exit += (_, _) => _taskbarAutoHide.RestoreIfNeeded();
        RefreshThumbnailSettingsStatus();

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
        _preferencesService.SetLanguage(code);
        CurrentLanguageCode = _preferencesService.CurrentLanguageCode;
        RefreshThumbnailSettingsStatus();
    }

    [RelayCommand]
    private void SelectFullscreenAnimation(string code)
    {
        CurrentAnimationCode = code;
        _preferencesService.SetFullscreenAnimation(code);
    }

    [RelayCommand]
    private void SelectThumbnailPerformanceMode(string code)
    {
        _preferencesService.SetThumbnailPerformanceMode(code);
        CurrentThumbnailPerformanceModeCode = _preferencesService.CurrentThumbnailPerformanceModeCode;
        _thumbnailGenerator.RefreshPerformanceMode();
        RefreshThumbnailSettingsStatus();
    }

    [RelayCommand]
    private void ToggleThumbnailGenerationPaused()
    {
        bool paused = !_preferencesService.IsThumbnailGenerationPaused;
        _preferencesService.SetThumbnailGenerationPaused(paused);
        _thumbnailGenerator.RefreshGenerationPaused();
        RefreshThumbnailSettingsStatus();
    }

    [RelayCommand]
    private void SelectThumbnailAccelerationMode(string code)
    {
        _preferencesService.SetThumbnailAccelerationMode(code);
        CurrentThumbnailAccelerationModeCode = _preferencesService.CurrentThumbnailAccelerationModeCode;
        _thumbnailGenerator.RefreshDecodeStrategy();
        RefreshThumbnailSettingsStatus();
    }

    private void RefreshThumbnailSettingsStatus()
    {
        IsThumbnailGenerationPaused = _preferencesService.IsThumbnailGenerationPaused;
        ThumbnailPerformanceSummary = IsThumbnailGenerationPaused
            ? _loc["Settings.ThumbnailGeneration.Paused"]
            : _loc[$"Settings.ThumbnailPerformance.{CapitalizeCode(CurrentThumbnailPerformanceModeCode)}"];
        ThumbnailAccelerationSummary = _loc[$"Settings.ThumbnailAcceleration.{CapitalizeCode(CurrentThumbnailAccelerationModeCode)}"];
        ThumbnailGenerationToggleText = _loc[IsThumbnailGenerationPaused
            ? "Settings.ThumbnailGeneration.Resume"
            : "Settings.ThumbnailGeneration.Stop"];

        var status = _preferencesService.CurrentThumbnailDecodeStatus;
        ThumbnailDetectedHardwareSummary = BuildHardwareSummary(status);
        ThumbnailCurrentDecoderSummary = BuildCurrentDecoderSummary(status);
        ThumbnailFallbackChainSummary = string.Join(" -> ", status.StrategyChain.Select(FormatStrategyName));
        RefreshThumbnailGenerationStatus();
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

        ThumbnailGenerationCountText = $"{snapshot.ReadyCount} / {snapshot.TotalCount}";
        ThumbnailGenerationDetailText = string.Format(
            _loc["Settings.ThumbnailGeneration.Detail"],
            snapshot.ActiveWorkers,
            snapshot.PendingCount);
        ThumbnailGenerationProgressPercent = snapshot.TotalCount > 0
            ? (double)snapshot.ReadyCount / snapshot.TotalCount * 100
            : 0;
        ThumbnailGenerationTooltipText = string.Format(
            _loc["Settings.ThumbnailGeneration.Tooltip"],
            ThumbnailGenerationStatusText,
            snapshot.ReadyCount,
            snapshot.TotalCount,
            snapshot.ActiveWorkers,
            snapshot.PendingCount);

        string statusLog =
            $"code={ThumbnailGenerationStatusCode}, ready={snapshot.ReadyCount}, total={snapshot.TotalCount}, " +
            $"active={snapshot.ActiveWorkers}, pending={snapshot.PendingCount}, paused={snapshot.IsPaused}, playerActive={snapshot.IsPlayerActive}";
        if (!string.Equals(_lastThumbnailGenerationStatusLog, statusLog, StringComparison.Ordinal))
        {
            _lastThumbnailGenerationStatusLog = statusLog;
            Log.Debug($"Thumbnail generation UI status updated: {statusLog}");
        }
    }

    private void OnThumbnailGeneratorStatusChanged()
    {
        Application.Current.Dispatcher.BeginInvoke((Action)RefreshThumbnailGenerationStatus);
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



