using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public sealed class ShellThumbnailStatusService : IShellThumbnailStatusService
{
    private readonly IShellPreferencesService _preferencesService;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    public ShellThumbnailStatusService(
        IShellPreferencesService preferencesService,
        IThumbnailGenerator thumbnailGenerator)
    {
        _preferencesService = preferencesService;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public ShellThumbnailStatusSnapshot GetStatusSnapshot()
    {
        var decodeStatus = _preferencesService.GetSnapshot().ThumbnailDecodeStatus;
        var generationStatus = _thumbnailGenerator.GetStatusSnapshot();
        var generationStatusCode = generationStatus.IsPaused
            ? "paused"
            : generationStatus.ActiveWorkers > 0
                ? "generating"
                : generationStatus.PendingCount > 0
                    ? "waiting"
                    : generationStatus.ReadyCount >= generationStatus.TotalCount && generationStatus.TotalCount > 0
                        ? "complete"
                        : "idle";

        return new ShellThumbnailStatusSnapshot(
            decodeStatus,
            generationStatus,
            generationStatusCode);
    }

    public ShellThumbnailPanelSnapshot GetPanelSnapshot(ILocalizationService localization)
    {
        var snapshot = GetStatusSnapshot();
        var generation = snapshot.GenerationStatus;
        var backgroundTasks = generation.ActiveTasks
            .Take(2)
            .Select(task => new ShellThumbnailPanelTaskItemSnapshot(
                task.VideoName,
                FormatIntent(task.Intent, localization),
                task.IsSuspended
                    ? localization["Settings.ThumbnailGeneration.Status.Paused"]
                    : localization[$"Settings.ThumbnailGeneration.Status.{task.State}"],
                FormatTaskStatusCode(task),
                task.ProgressPercent,
                task.IsSuspended))
            .ToArray();

        string statusText = localization[$"Settings.ThumbnailGeneration.Status.{CapitalizeCode(snapshot.GenerationStatusCode)}"];
        string hardwareSummary = BuildHardwareSummary(snapshot.DecodeStatus, localization);
        string currentDecoderSummary = BuildCurrentDecoderSummary(snapshot.DecodeStatus);
        string fallbackChainSummary = string.Join(" -> ", snapshot.DecodeStatus.StrategyChain.Select(FormatStrategyName));
        string generationStatusColor = snapshot.GenerationStatusCode switch
        {
            "paused" => "#C62828",
            "generating" => "#007AFF",
            "waiting" => "#F39C12",
            "complete" => "#2E7D32",
            _ => "#8E8E93"
        };
        double generationProgressPercent = generation.TotalCount > 0
            ? (double)generation.ReadyCount / generation.TotalCount * 100
            : 0;
        string generationSummaryText = string.Format(localization["Settings.ThumbnailGeneration.Summary"], generation.ReadyCount, generation.TotalCount);
        string backgroundTasksHeaderText = localization["TitleBar.ThumbnailBackgroundTasks"];
        string statusLog =
            $"code={snapshot.GenerationStatusCode}, ready={generation.ReadyCount}, total={generation.TotalCount}, " +
            $"active={generation.ActiveWorkers}, pending={generation.PendingCount}, foregroundPending={generation.ForegroundPendingCount}, " +
            $"target={generation.CurrentTargetIntent ?? "none"}:{generation.CurrentTargetName ?? "none"}, " +
            $"paused={generation.IsPaused}, playerActive={generation.IsPlayerActive}";

        return new ShellThumbnailPanelSnapshot(
            hardwareSummary,
            currentDecoderSummary,
            fallbackChainSummary,
            snapshot.GenerationStatusCode,
            statusText,
            generationStatusColor,
            generationProgressPercent,
            generationSummaryText,
            backgroundTasksHeaderText,
            backgroundTasks,
            statusLog);
    }

    private static string BuildHardwareSummary(ThumbnailDecodeStatusSnapshot status, ILocalizationService localization)
    {
        List<string> items = [];
        if (status.SupportsCuda)
            items.Add("CUDA");
        if (status.SupportsQsv)
            items.Add("QSV");
        if (status.SupportsD3D11VA)
            items.Add("D3D11VA");

        return items.Count > 0 ? string.Join(", ", items) : localization["Settings.ThumbnailAcceleration.Hardware.None"];
    }

    private static string BuildCurrentDecoderSummary(ThumbnailDecodeStatusSnapshot status)
    {
        if (status.PreferredStrategy is not null)
            return FormatStrategyName(status.PreferredStrategy.Value);

        if (status.StrategyChain.Count > 0)
            return FormatStrategyName(status.StrategyChain[0]);

        return FormatStrategyName(ThumbnailDecodeStrategy.Software);
    }

    private static string FormatIntent(ThumbnailWorkIntent intent, ILocalizationService localization)
        => intent switch
        {
            ThumbnailWorkIntent.ManualSingle => localization["Settings.ThumbnailGeneration.Intent.Current"],
            ThumbnailWorkIntent.PlaybackCurrent => localization["Settings.ThumbnailGeneration.Intent.Now"],
            ThumbnailWorkIntent.PlaybackNearby => localization["Settings.ThumbnailGeneration.Intent.Nearby"],
            ThumbnailWorkIntent.ManualCollection => localization["Settings.ThumbnailGeneration.Intent.Manual"],
            ThumbnailWorkIntent.FocusedCollection => localization["Settings.ThumbnailGeneration.Intent.Focused"],
            _ => localization["Settings.ThumbnailGeneration.Intent.Background"]
        };

    private static string FormatTaskStatusCode(ThumbnailActiveTaskSnapshot snapshot)
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
}
