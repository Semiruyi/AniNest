using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell;

public partial class ShellThumbnailStatusPanelViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;
    private readonly IShellThumbnailStatusService _shellThumbnailStatusService;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IUiDispatcher _uiDispatcher;

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

    public ShellThumbnailStatusPanelViewModel(
        ILocalizationService localization,
        IShellThumbnailStatusService shellThumbnailStatusService,
        IThumbnailGenerator thumbnailGenerator,
        IUiDispatcher uiDispatcher)
    {
        _localization = localization;
        _shellThumbnailStatusService = shellThumbnailStatusService;
        _thumbnailGenerator = thumbnailGenerator;
        _uiDispatcher = uiDispatcher;

        Refresh();
        _thumbnailGenerator.StatusChanged += OnThumbnailGeneratorStatusChanged;
    }

    public ObservableCollection<ThumbnailBackgroundTaskItemViewModel> ThumbnailBackgroundTasks { get; } = new();

    public void Refresh()
    {
        var panel = _shellThumbnailStatusService.GetPanelSnapshot(_localization);
        ThumbnailDetectedHardwareSummary = panel.HardwareSummary;
        ThumbnailCurrentDecoderSummary = panel.CurrentDecoderSummary;
        ThumbnailFallbackChainSummary = panel.FallbackChainSummary;
        ThumbnailGenerationStatusCode = panel.GenerationStatusCode;
        ThumbnailGenerationStatusText = panel.GenerationStatusText;
        ThumbnailGenerationStatusColor = panel.GenerationStatusColor;
        ThumbnailGenerationProgressPercent = panel.GenerationProgressPercent;
        ThumbnailGenerationSummaryText = panel.GenerationSummaryText;
        ThumbnailBackgroundTasksHeaderText = panel.BackgroundTasksHeaderText;
        ThumbnailBackgroundTasks.Clear();
        foreach (var task in panel.BackgroundTasks)
            ThumbnailBackgroundTasks.Add(new ThumbnailBackgroundTaskItemViewModel(task));
    }

    private void OnThumbnailGeneratorStatusChanged()
    {
        _uiDispatcher.BeginInvoke(Refresh);
    }

    public sealed class ThumbnailBackgroundTaskItemViewModel
    {
        public ThumbnailBackgroundTaskItemViewModel(ShellThumbnailPanelTaskItemSnapshot snapshot)
        {
            FileName = snapshot.FileName;
            IntentText = snapshot.IntentText;
            StatusCode = snapshot.StatusCode;
            ProgressPercent = snapshot.ProgressPercent;
            IsSuspended = snapshot.IsSuspended;
            StatusText = snapshot.StatusText;
        }

        public string FileName { get; }
        public string IntentText { get; }
        public string StatusText { get; }
        public string StatusCode { get; }
        public int ProgressPercent { get; }
        public bool IsSuspended { get; }
    }
}
