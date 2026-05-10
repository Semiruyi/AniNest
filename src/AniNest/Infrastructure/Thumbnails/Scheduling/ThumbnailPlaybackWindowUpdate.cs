namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailPlaybackWindowUpdate
{
    public required string CurrentVideoPath { get; init; }
    public required IntentApplyOutcome CurrentOutcome { get; init; }
    public required string CandidateWindowSummary { get; init; }
    public string? KeepPlaybackWorkerVideoPath { get; init; }
    public IReadOnlyCollection<string> StalePlaybackWorkerVideoPaths { get; init; } = [];
    public string? ProtectedVideoPath { get; init; }
    public ThumbnailWorkIntent? LowerPriorityPreemptionIntent { get; init; }
    public int NearbyApplied { get; init; }
    public int NearbyReady { get; init; }
    public int NearbyHigherIntent { get; init; }
    public int NearbyMissing { get; init; }
    public int DemotedStalePlaybackIntents { get; init; }
    public int StalePlaybackWorkers { get; init; }
    public bool ShouldPreemptLowerPriority { get; init; }
}
