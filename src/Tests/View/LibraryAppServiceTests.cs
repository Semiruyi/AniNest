using FluentAssertions;
using Moq;
using AniNest.Features.Library.Services;
using AniNest.Features.Metadata;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class LibraryAppServiceTests
{
    [Fact]
    public async Task LoadLibraryAsync_UsesTrackingSnapshot()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"LibraryAppServiceTests_{Guid.NewGuid():N}.json");
        var folderPath = Path.Combine(Path.GetTempPath(), $"LibraryAppServiceFolder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folderPath);
        var settings = new SettingsService(settingsPath);
        settings.AddFolder(folderPath, "Folder");

        var thumbnailService = new Mock<ILibraryThumbnailService>();
        var trackingService = new Mock<ILibraryTrackingService>();
        var metadataQueryService = new Mock<IMetadataQueryService>();
        var metadataSyncService = new Mock<IMetadataSyncService>();
        trackingService
            .Setup(service => service.GetFolderTrackingSnapshot(folderPath, It.IsAny<string[]>()))
            .Returns(new LibraryFolderTrackingSnapshot(1, WatchStatus.Completed, true));

        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(scanner => scanner.ScanFolderAsync(folderPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderScanResult(1, null, [Path.Combine(folderPath, "a.mp4")]));

        var service = new LibraryAppService(
            settings,
            thumbnailService.Object,
            trackingService.Object,
            metadataQueryService.Object,
            metadataSyncService.Object,
            videoScanner.Object);

        var items = await service.LoadLibraryAsync();

        items.Should().ContainSingle();
        items[0].PlayedCount.Should().Be(1);
        items[0].Status.Should().Be(WatchStatus.Completed);
        items[0].IsFavorite.Should().BeTrue();
        settings.Dispose();
        Directory.Delete(folderPath, true);
    }

    [Fact]
    public async Task LoadLibraryAsync_WithRealMetadataSync_WritesMetadataIndex()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"LibraryMetadataSync_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "settings.json");
        var indexPath = Path.Combine(directory, "metadata-index.json");
        var folderPath = Path.Combine(directory, "Folder");
        Directory.CreateDirectory(folderPath);

        var settings = new SettingsService(settingsPath);
        settings.AddFolder(folderPath, "Folder");

        var thumbnailService = new Mock<ILibraryThumbnailService>();
        var trackingService = new Mock<ILibraryTrackingService>();
        var metadataQueryService = new Mock<IMetadataQueryService>();
        trackingService
            .Setup(service => service.GetFolderTrackingSnapshot(folderPath, It.IsAny<string[]>()))
            .Returns(new LibraryFolderTrackingSnapshot(0, WatchStatus.Unsorted, false));

        var indexStore = new MetadataIndexStore(indexPath);
        var repository = new Mock<IMetadataRepository>();
        var taskStore = new MetadataTaskStore();
        var events = new MetadataEventHub();
        var metadataSyncService = new MetadataSyncCoordinator(indexStore, repository.Object, settings, taskStore, events);

        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(scanner => scanner.ScanFolderAsync(folderPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderScanResult(1, null, [Path.Combine(folderPath, "a.mp4")]));

        var service = new LibraryAppService(
            settings,
            thumbnailService.Object,
            trackingService.Object,
            metadataQueryService.Object,
            metadataSyncService,
            videoScanner.Object);

        await service.LoadLibraryAsync();

        File.Exists(indexPath).Should().BeTrue();
        var records = indexStore.Load();
        records.Should().ContainKey(folderPath);
        records[folderPath].State.Should().Be(MetadataState.Queued);

        settings.Dispose();
        Directory.Delete(directory, true);
    }
}
