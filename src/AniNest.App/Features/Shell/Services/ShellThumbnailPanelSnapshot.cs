namespace AniNest.Features.Shell.Services;

public sealed record ShellThumbnailPanelSnapshot(
    string HardwareSummary,
    string CurrentDecoderSummary,
    string FallbackChainSummary,
    string GenerationStatusCode,
    string GenerationStatusText,
    string GenerationStatusColor,
    double GenerationProgressPercent,
    string GenerationSummaryText,
    string BackgroundTasksHeaderText,
    IReadOnlyList<ShellThumbnailPanelTaskItemSnapshot> BackgroundTasks,
    string StatusLog);
