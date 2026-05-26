using AniNest.Application.Resources;

namespace AniNest.Host.Modules.Resources;

internal static class ResourceKindCodec
{
    public static string ToRouteSegment(ResourceKind kind)
        => kind switch
        {
            ResourceKind.LibraryCover => "library-cover",
            ResourceKind.LibraryPoster => "library-poster",
            ResourceKind.PlaybackMedia => "playback-media",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static bool TryParse(string value, out ResourceKind kind)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "library-cover":
                kind = ResourceKind.LibraryCover;
                return true;
            case "library-poster":
                kind = ResourceKind.LibraryPoster;
                return true;
            case "playback-media":
                kind = ResourceKind.PlaybackMedia;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
