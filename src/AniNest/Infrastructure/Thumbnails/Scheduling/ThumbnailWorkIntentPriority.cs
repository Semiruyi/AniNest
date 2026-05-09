namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailWorkIntentPriority
{
    public static int GetRank(ThumbnailWorkIntent intent)
        => intent switch
        {
            ThumbnailWorkIntent.ManualSingle => 5,
            ThumbnailWorkIntent.PlaybackCurrent => 4,
            ThumbnailWorkIntent.PlaybackNearby => 3,
            ThumbnailWorkIntent.ManualCollection => 2,
            ThumbnailWorkIntent.FocusedCollection => 1,
            _ => 0
        };

    public static bool IsPlaybackIntent(ThumbnailWorkIntent intent)
        => intent is ThumbnailWorkIntent.PlaybackCurrent or ThumbnailWorkIntent.PlaybackNearby;
}
