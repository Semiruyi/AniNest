using FluentAssertions;
using Moq;
using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.Model;

public class ShellThumbnailPerformanceAppServiceTests
{
    [Fact]
    public async Task TrySetPerformanceModeAsync_WhenRuntimeApplySucceeds_PersistsSetting()
    {
        var settings = new Mock<ISettingsService>();
        var thumbnailGenerator = new Mock<IThumbnailGenerator>();

        settings.Setup(x => x.GetThumbnailPerformanceMode()).Returns(ThumbnailPerformanceMode.Balanced);
        thumbnailGenerator.Setup(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Fast)).Returns(true);

        var service = new ShellThumbnailPerformanceAppService(
            settings.Object,
            thumbnailGenerator.Object);

        bool applied = await service.TrySetPerformanceModeAsync("fast");

        applied.Should().BeTrue();
        thumbnailGenerator.Verify(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Fast), Times.Once);
        settings.Verify(x => x.SetThumbnailPerformanceMode(ThumbnailPerformanceMode.Fast), Times.Once);
    }

    [Fact]
    public async Task TrySetPerformanceModeAsync_WhenRuntimeApplyFails_DoesNotPersistSetting()
    {
        var settings = new Mock<ISettingsService>();
        var thumbnailGenerator = new Mock<IThumbnailGenerator>();

        settings.Setup(x => x.GetThumbnailPerformanceMode()).Returns(ThumbnailPerformanceMode.Balanced);
        thumbnailGenerator.Setup(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Paused)).Returns(false);

        var service = new ShellThumbnailPerformanceAppService(
            settings.Object,
            thumbnailGenerator.Object);

        bool applied = await service.TrySetPerformanceModeAsync("paused");

        applied.Should().BeFalse();
        settings.Verify(x => x.SetThumbnailPerformanceMode(It.IsAny<ThumbnailPerformanceMode>()), Times.Never);
    }

    [Fact]
    public async Task TrySetPerformanceModeAsync_WhenPersistFails_RollsBackRuntime()
    {
        var settings = new Mock<ISettingsService>();
        var thumbnailGenerator = new Mock<IThumbnailGenerator>();

        settings.Setup(x => x.GetThumbnailPerformanceMode()).Returns(ThumbnailPerformanceMode.Balanced);
        settings.Setup(x => x.SetThumbnailPerformanceMode(ThumbnailPerformanceMode.Fast)).Throws(new InvalidOperationException("save failed"));
        thumbnailGenerator.Setup(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Fast)).Returns(true);
        thumbnailGenerator.Setup(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Balanced)).Returns(true);

        var service = new ShellThumbnailPerformanceAppService(
            settings.Object,
            thumbnailGenerator.Object);

        bool applied = await service.TrySetPerformanceModeAsync("fast");

        applied.Should().BeFalse();
        thumbnailGenerator.Verify(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Fast), Times.Once);
        thumbnailGenerator.Verify(x => x.TryApplyPerformanceMode(ThumbnailPerformanceMode.Balanced), Times.Once);
    }
}
