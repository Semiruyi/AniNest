using FluentAssertions;
using AniNest.Features.Library.Services;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;
using Moq;

namespace AniNest.Tests.View;

public class LibraryTrackingServiceTests
{
    [Fact]
    public void GetFolderTrackingSnapshot_ReadsPlayedCountAndClassification()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"LibraryTrackingServiceTests_{Guid.NewGuid():N}.json");
        var settings = new SettingsService(settingsPath);
        settings.AddFolder("/folder", "Folder");
        settings.SetFolderWatchStatus("/folder", WatchStatus.Watching);
        settings.SetFolderFavorite("/folder", true);
        settings.MarkVideoPlayed("/folder/a.mp4");

        var videoScanner = new Mock<IVideoScanner>();
        var service = new LibraryTrackingService(settings, videoScanner.Object);

        var snapshot = service.GetFolderTrackingSnapshot("/folder", ["/folder/a.mp4", "/folder/b.mp4"]);

        snapshot.PlayedCount.Should().Be(1);
        snapshot.Status.Should().Be(WatchStatus.Watching);
        snapshot.IsFavorite.Should().BeTrue();
        settings.Dispose();
    }

    [Fact]
    public async Task ClearFolderWatchHistoryAsync_ReturnsUpdatedFolderDto()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"LibraryTrackingServiceTests_{Guid.NewGuid():N}.json");
        var folderPath = Path.Combine(Path.GetTempPath(), $"LibraryTrackingFolder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folderPath);

        var settings = new SettingsService(settingsPath);
        settings.AddFolder(folderPath, "Folder");
        var videoPath = Path.Combine(folderPath, "a.mp4");
        settings.MarkVideoPlayed(videoPath);
        settings.SetFolderWatchStatus(folderPath, WatchStatus.Completed);
        settings.SetFolderFavorite(folderPath, true);

        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(scanner => scanner.ScanFolderAsync(folderPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderScanResult(1, null, [videoPath]));

        var service = new LibraryTrackingService(settings, videoScanner.Object);

        var result = await service.ClearFolderWatchHistoryAsync(folderPath);

        result.Should().NotBeNull();
        result!.PlayedCount.Should().Be(0);
        result.Status.Should().Be(WatchStatus.Completed);
        result.IsFavorite.Should().BeTrue();

        settings.Dispose();
        Directory.Delete(folderPath, true);
    }
}
