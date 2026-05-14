using System.ComponentModel;
using FluentAssertions;
using Moq;
using AniNest.Features.Player;
using AniNest.Features.Player.Input;
using AniNest.Features.Player.Playback;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.View;

public class PlayerViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public PlayerViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PlayerViewModelTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task PlayEpisode_WhenPlaybackFails_ShowsErrorDialog()
    {
        File.WriteAllText(Path.Combine(_tempDir, "ep01.mp4"), string.Empty);

        var playbackEngine = new Mock<IPlaybackEngine>();
        playbackEngine.Setup(controller => controller.TryLoad(It.IsAny<string>(), It.IsAny<long>(), out It.Ref<string?>.IsAny))
            .Returns((string _, long _, out string? errorMessage) =>
            {
                errorMessage = "decoder failed";
                return false;
            });

        var syncService = new Mock<IPlayerPlaybackStateSyncService>();
        var playbackFacade = new Mock<IPlayerPlaybackFacade>();
        var videoSurfaceSource = new Mock<IWpfVideoSurfaceSource>();
        var inputService = new Mock<IPlayerInputService>();
        var dialogs = new Mock<IDialogService>();
        var localization = CreateLocalizationService();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetPlayerVolume()).Returns(70);
        settings.Setup(service => service.GetPlayerMuted()).Returns(false);
        playbackFacade.SetupGet(service => service.Rate).Returns(1.0f);
        playbackFacade.SetupProperty(service => service.Volume, 70);
        playbackFacade.SetupProperty(service => service.IsMuted, false);

        var playlistService = new PlayerPlaylistService(
            settings.Object,
            playbackEngine.Object,
            new VideoScanner(),
            localization.Object,
            playbackFacade.Object);
        var session = new PlayerSessionController(Mock.Of<IPlayerThumbnailSyncService>(), playlistService);
        var playback = new PlayerPlaybackStateController(playbackEngine.Object, syncService.Object);
        var viewModel = new PlayerViewModel(
            session,
            playback,
            playbackFacade.Object,
            videoSurfaceSource.Object,
            localization.Object,
            settings.Object,
            Mock.Of<IUiDispatcher>(),
            dialogs.Object,
            inputService.Object);

        await playlistService.LoadFolderSkeletonAsync(_tempDir, "Test", CancellationToken.None);
        await playlistService.LoadFolderDataAsync(CancellationToken.None);

        playlistService.Playlist.Items.Should().ContainSingle();
        playlistService.ActivateCurrentVideo();

        dialogs.Verify(service => service.ShowError(
            "Could not play \"ep01.mp4\".\ndecoder failed",
            "Playback Failed"), Times.Once);
    }

    private static Mock<ILocalizationService> CreateLocalizationService()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.CurrentLanguage).Returns("en-US");
        localization.Setup(service => service.AvailableLanguages).Returns(Array.Empty<LanguageInfo>());
        localization.Setup(service => service[It.IsAny<string>()]).Returns((string key) => key switch
        {
            "Player.PlaybackFailed.Title" => "Playback Failed",
            "Player.PlaybackFailed.Message" => "Could not play \"{0}\".\n{1}",
            "Dialog.UnknownError" => "Unknown error",
            _ => key
        });
        localization.SetupAdd(service => service.PropertyChanged += It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });
        localization.SetupRemove(service => service.PropertyChanged -= It.IsAny<PropertyChangedEventHandler>())
            .Callback<PropertyChangedEventHandler>(_ => { });
        return localization;
    }
}
