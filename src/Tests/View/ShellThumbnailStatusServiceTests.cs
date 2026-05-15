using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class ShellThumbnailStatusServiceTests
{
    [Fact]
    public void GetStatusSnapshot_WhenPaused_ReturnsPausedCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            true, false, 0, 5, 10, 2, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("paused");
    }

    [Fact]
    public void GetStatusSnapshot_WhenGenerating_ReturnsGeneratingCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 1, 5, 10, 2, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("generating");
    }

    [Fact]
    public void GetStatusSnapshot_WhenWaiting_ReturnsWaitingCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 0, 5, 10, 2, 3, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("waiting");
    }

    [Fact]
    public void GetStatusSnapshot_WhenComplete_ReturnsCompleteCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 0, 10, 10, 0, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("complete");
    }

    [Fact]
    public void GetStatusSnapshot_WhenIdle_ReturnsIdleCode()
    {
        var service = CreateService(new ThumbnailGenerationStatusSnapshot(
            false, false, 0, 0, 0, 0, 0, null, null, Array.Empty<ThumbnailActiveTaskSnapshot>()));

        var snapshot = service.GetStatusSnapshot();

        snapshot.GenerationStatusCode.Should().Be("idle");
    }

    private static ShellThumbnailStatusService CreateService(ThumbnailGenerationStatusSnapshot generationStatus)
    {
        var preferences = new Mock<IShellPreferencesService>();
        preferences.SetupGet(service => service.CurrentThumbnailDecodeStatus).Returns(
            new ThumbnailDecodeStatusSnapshot(
                ThumbnailAccelerationMode.Auto,
                new[] { ThumbnailDecodeStrategy.NvidiaCuda, ThumbnailDecodeStrategy.Software },
                ThumbnailDecodeStrategy.NvidiaCuda,
                true,
                true,
                false));

        var generator = new Mock<IThumbnailGenerator>();
        generator.Setup(service => service.GetStatusSnapshot()).Returns(generationStatus);

        return new ShellThumbnailStatusService(preferences.Object, generator.Object);
    }
}
