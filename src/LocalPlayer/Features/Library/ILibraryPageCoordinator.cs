using System.Collections.Generic;
using LocalPlayer.Features.Library.Models;

namespace LocalPlayer.Features.Library;

public interface ILibraryPageCoordinator
{
    List<(FolderListItem Item, string[] VideoFiles)> LoadFoldersData();
    void EnqueueAllFolders(IEnumerable<(FolderListItem Item, string[] VideoFiles)> items);
    void EnqueueFolderForThumbnails(FolderListItem item, string[] videoFiles);
    void ReorderFolders(List<string> orderedPaths);
    void DeleteFolder(FolderListItem item);
    FolderListItem CreateFolderItem(string name, string path, int videoCount, string? coverPath);
}
