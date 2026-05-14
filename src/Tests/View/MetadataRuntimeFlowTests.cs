using AniNest.Features.Metadata;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;
using FluentAssertions;
using Moq;

namespace AniNest.Tests.View;

public class MetadataRuntimeFlowTests
{
    [Fact]
    public async Task SyncThenWorker_StartsConsumingQueuedRecords()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataRuntimeFlow_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "settings.json");
        var indexPath = Path.Combine(directory, "index.json");

        var settings = new SettingsService(settingsPath);
        var indexStore = new MetadataIndexStore(indexPath);
        var repository = new Mock<IMetadataRepository>();
        var taskStore = new MetadataTaskStore();
        var events = new MetadataEventHub();
        var sync = new MetadataSyncCoordinator(indexStore, repository.Object, settings, taskStore, events);

        var imageCache = new Mock<IMetadataImageCache>();
        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(service => service.GetVideoFilesAsync("/folder", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["/folder/a.mp4"]);
        var provider = new Mock<IMetadataProvider>();
        provider.Setup(service => service.FetchAsync(It.IsAny<MetadataFolderRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataFetchResult.NetworkError());

        using var summaryChanged = new ManualResetEventSlim(false);
        events.SummaryChanged += (_, args) =>
        {
            if (args.Summary.NetworkErrorCount > 0 || args.Summary.NeedsMetadataCount > 0)
                summaryChanged.Set();
        };

        using var worker = new MetadataWorker(taskStore, indexStore, repository.Object, imageCache.Object, provider.Object, videoScanner.Object, events);
        worker.Start();

        await sync.SyncLibrarySnapshotAsync(
        [
            new MetadataFolderRef("/folder", "Folder", ["/folder/a.mp4"])
        ]);

        summaryChanged.Wait(TimeSpan.FromSeconds(3)).Should().BeTrue();

        var records = indexStore.Load();
        records.Should().ContainKey("/folder");
        records["/folder"].State.Should().Be(MetadataState.NeedsMetadata);
        records["/folder"].FailureKind.Should().Be(MetadataFailureKind.NetworkError);
        records["/folder"].LastAttemptAtUtc.Should().NotBeNull();

        settings.Dispose();
        Directory.Delete(directory, true);
    }
}
