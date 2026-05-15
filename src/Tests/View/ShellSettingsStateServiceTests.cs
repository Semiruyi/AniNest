using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class ShellSettingsStateServiceTests
{
    [Fact]
    public void GetStateSnapshot_MapsEnglishAndContinuousSelections()
    {
        var service = CreateService(new ShellPreferencesSnapshot(
            "en-US",
            "continuous",
            "fast",
            "compatible",
            CreateDecodeStatus()));

        var snapshot = service.GetStateSnapshot();

        snapshot.LanguageSelectedIndex.Should().Be(1);
        snapshot.IsLanguageEnglishSelected.Should().BeTrue();
        snapshot.IsLanguageChineseSelected.Should().BeFalse();
        snapshot.FullscreenAnimationSelectedIndex.Should().Be(1);
        snapshot.IsFullscreenAnimationContinuousSelected.Should().BeTrue();
        snapshot.ThumbnailPerformanceSelectedIndex.Should().Be(3);
        snapshot.IsThumbnailPerformanceFastSelected.Should().BeTrue();
        snapshot.ThumbnailAccelerationSelectedIndex.Should().Be(1);
        snapshot.IsThumbnailAccelerationCompatibleSelected.Should().BeTrue();
    }

    [Fact]
    public void GetStateSnapshot_MapsDefaultSelections()
    {
        var service = CreateService(new ShellPreferencesSnapshot(
            "zh-CN",
            "none",
            "balanced",
            "auto",
            CreateDecodeStatus()));

        var snapshot = service.GetStateSnapshot();

        snapshot.LanguageSelectedIndex.Should().Be(0);
        snapshot.IsLanguageChineseSelected.Should().BeTrue();
        snapshot.FullscreenAnimationSelectedIndex.Should().Be(0);
        snapshot.IsFullscreenAnimationNoneSelected.Should().BeTrue();
        snapshot.ThumbnailPerformanceSelectedIndex.Should().Be(2);
        snapshot.IsThumbnailPerformanceBalancedSelected.Should().BeTrue();
        snapshot.IsThumbnailPerformancePaused.Should().BeFalse();
        snapshot.ThumbnailAccelerationSelectedIndex.Should().Be(0);
        snapshot.IsThumbnailAccelerationAutoSelected.Should().BeTrue();
    }

    [Fact]
    public void GetStateSnapshot_MapsPausedPerformance()
    {
        var service = CreateService(new ShellPreferencesSnapshot(
            "zh-CN",
            "none",
            "paused",
            "auto",
            CreateDecodeStatus()));

        var snapshot = service.GetStateSnapshot();

        snapshot.ThumbnailPerformanceSelectedIndex.Should().Be(0);
        snapshot.IsThumbnailPerformancePausedSelected.Should().BeTrue();
        snapshot.IsThumbnailPerformancePaused.Should().BeTrue();
    }

    private static ShellSettingsStateService CreateService(ShellPreferencesSnapshot preferencesSnapshot)
    {
        var preferencesService = new Mock<IShellPreferencesService>();
        preferencesService.Setup(service => service.GetSnapshot()).Returns(preferencesSnapshot);
        return new ShellSettingsStateService(preferencesService.Object);
    }

    private static ThumbnailDecodeStatusSnapshot CreateDecodeStatus()
        => new(
            ThumbnailAccelerationMode.Auto,
            new[] { ThumbnailDecodeStrategy.Software },
            ThumbnailDecodeStrategy.Software,
            false,
            false,
            false);
}
