using AniNest.Features.Shell.Services;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class ShellSettingsOptionsPanelServiceTests
{
    [Fact]
    public void GetOptionsPanelSnapshot_MapsLocalizedOptionLabelsAndSelectionState()
    {
        var preferencesService = new Mock<IShellPreferencesService>();
        preferencesService.Setup(service => service.GetSnapshot()).Returns(
            new ShellPreferencesSnapshot(
                "en-US",
                "continuous",
                "balanced",
                "auto",
                new ThumbnailDecodeStatusSnapshot(
                    ThumbnailAccelerationMode.Auto,
                    new[] { ThumbnailDecodeStrategy.Software },
                    ThumbnailDecodeStrategy.Software,
                    false,
                    false,
                    false)));

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service[It.IsAny<string>()]).Returns((string key) => key switch
        {
            "Settings.FullscreenAnimation.NoAnimation" => "No animation",
            "Settings.FullscreenAnimation.ContinuousAnimation" => "Continuous",
            _ => key
        });

        var service = new ShellSettingsStateService(preferencesService.Object);

        var snapshot = service.GetOptionsPanelSnapshot(localization.Object);

        snapshot.LanguageOptions.Should().HaveCount(2);
        snapshot.LanguageOptions[0].Code.Should().Be("zh-CN");
        snapshot.LanguageOptions[0].DisplayName.Should().Be("简体中文");
        snapshot.LanguageOptions[0].IsSelected.Should().BeFalse();
        snapshot.LanguageOptions[1].Code.Should().Be("en-US");
        snapshot.LanguageOptions[1].IsSelected.Should().BeTrue();

        snapshot.FullscreenAnimationOptions.Should().HaveCount(2);
        snapshot.FullscreenAnimationOptions[0].DisplayName.Should().Be("No animation");
        snapshot.FullscreenAnimationOptions[0].IsSelected.Should().BeFalse();
        snapshot.FullscreenAnimationOptions[1].DisplayName.Should().Be("Continuous");
        snapshot.FullscreenAnimationOptions[1].IsSelected.Should().BeTrue();
    }
}
