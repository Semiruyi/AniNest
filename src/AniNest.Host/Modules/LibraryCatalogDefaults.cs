using AniNest.Application.Library;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal static class LibraryCatalogDefaults
{
    public static IReadOnlyList<LibraryFolderRecord> CreateFolders()
    {
        return
        [
            new(
                "sample-folder",
                "Sample Anime",
                "D:/Media/Sample Anime",
                12,
                null,
                new LibraryMetadataSummaryDto("Sample Anime", null),
                0)
        ];
    }

    public static IReadOnlyDictionary<string, WatchStatus> CreateWatchStatuses()
        => new Dictionary<string, WatchStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["sample-folder"] = WatchStatus.Watching
        };

    public static IReadOnlyDictionary<string, bool> CreateFavorites()
        => new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["sample-folder"] = true
        };
}
