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

    [Fact]
    public void GetFolderSummary_ComputesCountsAndCompletion()
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var store = new InMemoryThumbnailStore(
        [
            new ThumbnailRecord("folder-01", "ep-01", ThumbnailState.Ready, 100, "thumb-01.jpg", updatedAt.AddMinutes(-1)),
            new ThumbnailRecord("folder-01", "ep-02", ThumbnailState.Generating, 25, null, updatedAt),
            new ThumbnailRecord("folder-01", "ep-03", ThumbnailState.Failed, 0, null, updatedAt.AddMinutes(-2))
        ]);
        var service = new ThumbnailService(store);

        var summary = service.GetFolderSummary("folder-01", totalItems: 4);

        Assert.Equal("folder-01", summary.FolderId);
        Assert.Equal(4, summary.Total);
        Assert.Equal(0, summary.Pending);
        Assert.Equal(1, summary.Generating);
        Assert.Equal(1, summary.Ready);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(25, summary.CompletionPercent);
        Assert.Equal(updatedAt, summary.UpdatedAtUtc);
    }
}
