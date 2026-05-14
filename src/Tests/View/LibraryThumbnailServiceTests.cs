using Moq;
using AniNest.Features.Library.Services;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class LibraryThumbnailServiceTests
{
    [Fact]
    public async Task FocusFolderThumbnailsAsync_ForwardsToFocusCollection()
    {
        var thumbnailGenerator = new Mock<IThumbnailGenerator>();
        var videoScanner = new Mock<IVideoScanner>();
        videoScanner.Setup(scanner => scanner.GetVideoFilesAsync("/folder", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["/folder/a.mp4"]);

        var service = new LibraryThumbnailService(thumbnailGenerator.Object, videoScanner.Object);

        await service.FocusFolderThumbnailsAsync("/folder");

        thumbnailGenerator.Verify(generator => generator.RegisterCollection(
            It.Is<LibraryCollectionRef>(collection => collection.Id == "/folder"),
            It.Is<IReadOnlyCollection<string>>(videos => videos.Count == 1 && videos.Contains("/folder/a.mp4"))), Times.Once);
        thumbnailGenerator.Verify(generator => generator.FocusCollection("/folder"), Times.Once);
    }
}
