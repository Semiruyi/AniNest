using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal static class MetadataDefaults
{
    public static IReadOnlyList<MetadataDto> Create()
    {
        return
        [
            new(
                "sample-folder",
                "Sample Anime",
                "Sample Anime",
                "A sample metadata record used by the backend scaffold.",
                ["slice-of-life"],
                null,
                "S1",
                12,
                "local",
                MetadataState.Ready,
                MetadataFailureKind.None)
        ];
    }
}
