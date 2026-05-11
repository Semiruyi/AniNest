namespace AniNest.Infrastructure.Thumbnails;

public enum ThumbnailPerformanceMode
{
    Paused,
    Quiet,
    Balanced,
    Fast
}

public readonly record struct ThumbnailExecutionPolicy(
    int MaxConcurrency,
    bool AllowStartNewJobs);

public static class ThumbnailPerformancePolicy
{
    public static ThumbnailExecutionPolicy Create(ThumbnailPerformanceMode mode, bool isPlayerActive)
        => mode switch
        {
            ThumbnailPerformanceMode.Paused => new ThumbnailExecutionPolicy(0, false),
            ThumbnailPerformanceMode.Quiet => isPlayerActive
                ? new ThumbnailExecutionPolicy(0, false)
                : new ThumbnailExecutionPolicy(1, true),
            ThumbnailPerformanceMode.Fast => isPlayerActive
                ? new ThumbnailExecutionPolicy(1, true)
                : new ThumbnailExecutionPolicy(2, true),
            _ => isPlayerActive
                ? new ThumbnailExecutionPolicy(1, false)
                : new ThumbnailExecutionPolicy(1, true)
        };
}
