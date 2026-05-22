using AniNest.Application.Library;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class FileLibraryCatalogStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = CreateStore(CreateTempPath());

        var folders = store.GetFolders();

        Assert.Single(folders);
        Assert.Equal("sample-folder", folders[0].FolderId);
        Assert.Equal(WatchStatus.Watching, store.GetWatchStatus("sample-folder"));
        Assert.True(store.GetIsFavorite("sample-folder"));
    }

    [Fact]
    public void SaveFolders_AndFlags_PersistToDisk()
    {
        var path = CreateTempPath();
        var store = CreateStore(path);

        store.SaveFolders(
        [
            new LibraryFolderRecord("folder-01", "Folder 01", "D:/Anime/Folder 01", 12, null, null, 0),
            new LibraryFolderRecord("folder-02", "Folder 02", "D:/Anime/Folder 02", 24, null, null, 1)
        ]);
        store.SetWatchStatus("folder-02", WatchStatus.Completed);
        store.SetIsFavorite("folder-02", true);

        var reloaded = CreateStore(path);
        var folders = reloaded.GetFolders();
        Assert.Equal(2, folders.Count);
        Assert.Equal("folder-02", folders[1].FolderId);
        Assert.Equal(WatchStatus.Completed, reloaded.GetWatchStatus("folder-02"));
        Assert.True(reloaded.GetIsFavorite("folder-02"));
    }

    private static FileLibraryCatalogStore CreateStore(string path)
        => new(
            path,
            LibraryCatalogDefaults.CreateFolders(),
            LibraryCatalogDefaults.CreateWatchStatuses(),
            LibraryCatalogDefaults.CreateFavorites());

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}.library.json");
}
