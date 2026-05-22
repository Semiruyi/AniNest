using AniNest.Application.Thumbnail;
using AniNest.Core.Enums;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class FileThumbnailStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new FileThumbnailStore(CreateTempPath(), ThumbnailDefaults.Create());

        var all = store.GetAll();

        Assert.Empty(all);
    }

    [Fact]
    public void SaveMany_AndClear_PersistAndRemoveFolderRecords()
    {
        var path = CreateTempPath();
        var store = new FileThumbnailStore(path, ThumbnailDefaults.Create());

        store.SaveMany(
        [
            new ThumbnailRecord("folder-01", "ep-01", ThumbnailState.Generating, 10, null, DateTimeOffset.UtcNow),
            new ThumbnailRecord("folder-01", "ep-02", ThumbnailState.Ready, 100, "thumb-02.jpg", DateTimeOffset.UtcNow)
        ]);

        var reloaded = new FileThumbnailStore(path, ThumbnailDefaults.Create());
        Assert.Equal(2, reloaded.GetByFolderId("folder-01").Count);

        reloaded.DeleteByFolderId("folder-01");

        var cleared = new FileThumbnailStore(path, ThumbnailDefaults.Create());
        Assert.Empty(cleared.GetByFolderId("folder-01"));
    }

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}.thumbnails.json");
}
