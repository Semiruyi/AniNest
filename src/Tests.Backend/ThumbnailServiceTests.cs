using AniNest.Application.Thumbnail;
using AniNest.Core.Enums;

namespace AniNest.Backend.Tests;

public sealed class ThumbnailServiceTests
{
    [Fact]
    public void GetByFolderId_ReturnsMappedStatuses()
    {
        var store = new InMemoryThumbnailStore(
        [
            new ThumbnailRecord("folder-01", "ep-01", ThumbnailState.Ready, 100, "thumb.jpg", DateTimeOffset.UtcNow)
        ]);
        var service = new ThumbnailService(store);

        var result = service.GetByFolderId("folder-01");

        Assert.Single(result);
        Assert.Equal("ep-01", result[0].TargetId);
        Assert.Equal(ThumbnailState.Ready, result[0].State);
    }

    [Fact]
    public void ClearFolder_RemovesFolderRecords()
    {
        var store = new InMemoryThumbnailStore(
        [
            new ThumbnailRecord("folder-01", "ep-01", ThumbnailState.Pending, 0, null, null)
        ]);
        var service = new ThumbnailService(store);

        service.ClearFolder("folder-01");

        Assert.Empty(service.GetByFolderId("folder-01"));
    }
}
