using AniNest.Application.Library;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Backend.Tests;

public sealed class LibraryCatalogServiceTests
{
    [Fact]
    public void AddFolder_AppendsNewFolder()
    {
        var store = CreateStore();
        var service = new LibraryCatalogService(store);

        service.AddFolder(new AddLibraryFolderRequest("D:/Anime/Bocchi The Rock"));

        var folders = service.GetFolders();
        Assert.Equal(3, folders.Count);
        Assert.Contains(folders, folder => folder.FolderId == "bocchi-the-rock");
    }

    [Fact]
    public void SetFavorite_UpdatesFolderState()
    {
        var store = CreateStore();
        var service = new LibraryCatalogService(store);

        service.SetFavorite("folder-02", true);

        var folder = service.GetFolders().Single(item => item.FolderId == "folder-02");
        Assert.True(folder.IsFavorite);
    }

    [Fact]
    public void SetWatchStatus_UpdatesFolderState()
    {
        var store = CreateStore();
        var service = new LibraryCatalogService(store);

        service.SetWatchStatus("folder-02", WatchStatus.Completed);

        var folder = service.GetFolders().Single(item => item.FolderId == "folder-02");
        Assert.Equal(WatchStatus.Completed, folder.WatchStatus);
    }

    [Fact]
    public void MoveFolderToFront_ReordersFolders()
    {
        var store = CreateStore();
        var service = new LibraryCatalogService(store);

        service.MoveFolderToFront("folder-02");

        var folders = service.GetFolders();
        Assert.Equal("folder-02", folders[0].FolderId);
    }

    [Fact]
    public void DeleteFolder_RemovesAndReordersRemainingFolders()
    {
        var store = CreateStore();
        var service = new LibraryCatalogService(store);

        service.DeleteFolder("folder-01");

        var folders = service.GetFolders();
        Assert.Single(folders);
        Assert.Equal("folder-02", folders[0].FolderId);
    }

    private static InMemoryLibraryCatalogStore CreateStore()
    {
        return new InMemoryLibraryCatalogStore(
        [
            new LibraryFolderRecord("folder-01", "Folder 01", "D:/Anime/Folder 01", 12, null, null, 0),
            new LibraryFolderRecord("folder-02", "Folder 02", "D:/Anime/Folder 02", 24, null, null, 1)
        ]);
    }
}
