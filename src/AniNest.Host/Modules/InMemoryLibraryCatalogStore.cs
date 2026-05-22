using AniNest.Application.Library;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class InMemoryLibraryCatalogStore : ILibraryCatalogStore
{
    private readonly Dictionary<string, WatchStatus> _watchStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private List<LibraryFolderRecord> _folders =
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

    public InMemoryLibraryCatalogStore()
    {
        _watchStatuses["sample-folder"] = WatchStatus.Watching;
        _favorites["sample-folder"] = true;
    }

    public IReadOnlyList<LibraryFolderRecord> GetFolders()
        => _folders.ToArray();

    public void SaveFolders(IReadOnlyList<LibraryFolderRecord> folders)
    {
        _folders = folders.ToList();
    }

    public WatchStatus GetWatchStatus(string folderId)
        => _watchStatuses.TryGetValue(folderId, out var status) ? status : WatchStatus.Unknown;

    public void SetWatchStatus(string folderId, WatchStatus status)
    {
        _watchStatuses[folderId] = status;
    }

    public bool GetIsFavorite(string folderId)
        => _favorites.TryGetValue(folderId, out var isFavorite) && isFavorite;

    public void SetIsFavorite(string folderId, bool isFavorite)
    {
        _favorites[folderId] = isFavorite;
    }
}
