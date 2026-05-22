using AniNest.Application.Thumbnail;

namespace AniNest.Host.Modules;

internal static class ThumbnailDefaults
{
    public static IReadOnlyList<ThumbnailRecord> Create()
        => Array.Empty<ThumbnailRecord>();
}
