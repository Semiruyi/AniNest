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
}
