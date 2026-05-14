using System.ComponentModel;
using FluentAssertions;
using Moq;
using AniNest.Features.Player;
using AniNest.Features.Player.Playback;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;

namespace AniNest.Tests.View;

public class ControlBarViewModelTests
{
    [Fact]
    public void ChangeVolume_UpdatesPlaybackAndSettings()
    {
        var playbackFacade = new Mock<IPlayerPlaybackFacade>();
        playbackFacade.SetupProperty(service => service.Volume, 70);
        playbackFacade.SetupProperty(service => service.IsMuted, false);
        playbackFacade.SetupGet(service => service.Rate).Returns(1.0f);

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetPlayerVolume()).Returns(70);
        settings.Setup(service => service.GetPlayerMuted()).Returns(false);

        var viewModel = CreateViewModel(playbackFacade.Object, settings.Object);

        viewModel.ChangeVolumeCommand.Execute(25d);

        viewModel.Volume.Should().Be(25);
        viewModel.IsMuted.Should().BeFalse();
        playbackFacade.VerifySet(service => service.Volume = 25, Times.Once);
        playbackFacade.VerifySet(service => service.IsMuted = false, Times.AtLeastOnce);
        settings.Verify(service => service.SetPlayerVolume(25), Times.Once);
        settings.Verify(service => service.SetPlayerMuted(false), Times.AtLeastOnce);
    }

    [Fact]
    public void ToggleMute_RestoresLastNonZeroVolume()
    {
        var playbackFacade = new Mock<IPlayerPlaybackFacade>();
        playbackFacade.SetupProperty(service => service.Volume, 40);
        playbackFacade.SetupProperty(service => service.IsMuted, false);
        playbackFacade.SetupGet(service => service.Rate).Returns(1.0f);

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetPlayerVolume()).Returns(40);
        settings.Setup(service => service.GetPlayerMuted()).Returns(false);

        var viewModel = CreateViewModel(playbackFacade.Object, settings.Object);

        viewModel.ToggleMuteCommand.Execute(null);
        viewModel.IsMuted.Should().BeTrue();

        viewModel.ToggleMuteCommand.Execute(null);

        viewModel.Volume.Should().Be(40);
        viewModel.IsMuted.Should().BeFalse();
        playbackFacade.VerifySet(service => service.IsMuted = true, Times.Once);
        playbackFacade.VerifySet(service => service.Volume = 40, Times.AtLeastOnce);
    }

    private static ControlBarViewModel CreateViewModel(
        IPlayerPlaybackFacade playbackFacade,
        ISettingsService settings)
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.CurrentLanguage).Returns("en-US");
        localization.Setup(service => service.AvailableLanguages).Returns(Array.Empty<LanguageInfo>());
        localization.Setup(service => service[It.IsAny<string>()]).Returns((string key) => key switch
        {
            "Player.PlayPause" => "Play / Pause",
            "Player.Previous" => "Previous",
            "Player.Next" => "Next",
            "Player.Mute" => "Mute",
            "Player.Unmute" => "Unmute",
            _ => key
        });
        localization.SetupAdd(service => service.PropertyChanged += It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });
        localization.SetupRemove(service => service.PropertyChanged -= It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });

        var playbackState = new PlayerPlaybackStateController(
            Mock.Of<IPlaybackEngine>(),
            Mock.Of<IPlayerPlaybackStateSyncService>());

        return new ControlBarViewModel(
            playbackFacade,
            localization.Object,
            settings,
            Mock.Of<IUiDispatcher>(),
            playbackState);
    }
}
