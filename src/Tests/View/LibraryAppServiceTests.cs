using FluentAssertions;
using Moq;
using AniNest.Features.Library.Services;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Media;

namespace AniNest.Tests.View;

public class LibraryAppServiceTests
{
    [Fact]
    public async Task FocusFolderThumbnailsAsync_ForwardsToFocusCollection()
    {
        var settings = new SettingsService(Path.Combine(Path.GetTempPath(), $"LibraryAppServiceTests_{Guid.NewGuid():N}.json"));
        var thumbnailGenerator = new Mock<IThumbnailGenerator>();
        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(scanner => scanner.GetVideoFilesAsync("/folder", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["/folder/a.mp4"]);

        var service = new LibraryAppService(settings, thumbnailGenerator.Object, videoScanner.Object);

        await service.FocusFolderThumbnailsAsync("/folder");

        thumbnailGenerator.Verify(generator => generator.FocusCollection("/folder"), Times.Once);
        settings.Dispose();
    }

    [Fact]
    public async Task LoadLibraryAsync_IncludesFolderClassification()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"LibraryAppServiceTests_{Guid.NewGuid():N}.json");
        var folderPath = Path.Combine(Path.GetTempPath(), $"LibraryAppServiceFolder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folderPath);
        var settings = new SettingsService(settingsPath);
        settings.AddFolder(folderPath, "Folder");
        settings.SetFolderWatchStatus(folderPath, WatchStatus.Completed);
        settings.SetFolderFavorite(folderPath, true);

        var thumbnailGenerator = new Mock<IThumbnailGenerator>();
        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(scanner => scanner.ScanFolderAsync(folderPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderScanResult(1, null, [Path.Combine(folderPath, "a.mp4")]));

        var service = new LibraryAppService(settings, thumbnailGenerator.Object, videoScanner.Object);

        var items = await service.LoadLibraryAsync();

        items.Should().ContainSingle();
        items[0].Status.Should().Be(WatchStatus.Completed);
        items[0].IsFavorite.Should().BeTrue();
        settings.Dispose();
        Directory.Delete(folderPath, true);
    }
}
