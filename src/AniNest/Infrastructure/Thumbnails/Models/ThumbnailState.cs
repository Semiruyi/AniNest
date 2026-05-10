namespace AniNest.Infrastructure.Thumbnails;

public enum ThumbnailState
{
    Pending,
    Generating,
    PausedGenerating,
    Ready,
    Failed
}
