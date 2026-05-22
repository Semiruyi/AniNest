using AniNest.Core.Enums;

namespace AniNest.Application.Library;

public interface ILibraryCatalogStore
{
    IReadOnlyList<LibraryFolderRecord> GetFolders();
    void SaveFolders(IReadOnlyList<LibraryFolderRecord> folders);
    WatchStatus GetWatchStatus(string folderId);
    void SetWatchStatus(string folderId, WatchStatus status);
    bool GetIsFavorite(string folderId);
    void SetIsFavorite(string folderId, bool isFavorite);
}
