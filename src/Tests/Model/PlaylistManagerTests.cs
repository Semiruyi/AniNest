using System.IO;
using FluentAssertions;
using AniNest.Features.Player.Models;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using Moq;
using Xunit;

namespace AniNest.Tests.Model;

public class PlaylistManagerTests : IDisposable
{
    private readonly Mock<ISettingsService> _settingsMock = new();
    private readonly Mock<IMediaPlayerController> _mediaMock = new();
    private readonly IVideoScanner _videoScanner = new VideoScanner();
    private readonly PlaylistManager _manager;
    private readonly string _tempDir;

    public PlaylistManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PlaylistManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _mediaMock
            .Setup(media => media.TryPlay(It.IsAny<string>(), It.IsAny<long>(), out It.Ref<string?>.IsAny))
            .Returns((string _, long _, out string? errorMessage) =>
            {
                errorMessage = null;
                return true;
            });

        _manager = new PlaylistManager(
            _settingsMock.Object,
            _mediaMock.Object,
            _videoScanner,
            _ => ThumbnailState.Pending);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void InitialState_Empty()
    {
        _manager.ItemCount.Should().Be(0);
        _manager.CurrentIndex.Should().Be(-1);
        _manager.CurrentItem.Should().BeNull();
    }

    [Fact]
    public async Task PlayNext_AtStart_AdvancesIndex()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");

        var result = _manager.PlayNext();

        result.Should().BeTrue();
        _manager.CurrentIndex.Should().Be(1);
    }

    [Fact]
    public async Task PlayNext_AtEnd_ReturnsFalse()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        // Move to last
        _manager.CurrentIndex = 2;

        var result = _manager.PlayNext();

        result.Should().BeFalse();
        _manager.CurrentIndex.Should().Be(2);
    }

    [Fact]
    public async Task PlayPrevious_AtStart_ReturnsFalse()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");

        var result = _manager.PlayPrevious();

        result.Should().BeFalse();
        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public async Task PlayPrevious_AtMiddle_GoesBack()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        _manager.CurrentIndex = 1;

        var result = _manager.PlayPrevious();

        result.Should().BeTrue();
        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public async Task PlayEpisode_ValidIndex_PlaysIt()
    {
        CreateVideoFiles(5);
        await _manager.LoadFolderAsync(_tempDir, "Test");

        _manager.PlayEpisode(3);

        _manager.CurrentIndex.Should().Be(3);
    }

    [Fact]
    public async Task PlayEpisode_OutOfRange_DoesNothing()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");

        _manager.PlayEpisode(10);

        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public async Task PlayEpisode_NegativeIndex_DoesNothing()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");

        _manager.PlayEpisode(-1);

        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void SaveProgress_CallsSetVideoProgress()
    {
        _mediaMock.Setup(m => m.CurrentFilePath).Returns("/videos/test.mp4");
        _mediaMock.Setup(m => m.Time).Returns(5000);
        _mediaMock.Setup(m => m.Length).Returns(60000);

        _manager.SaveProgress();

        _settingsMock.Verify(s => s.SetVideoProgress("/videos/test.mp4", 5000, 60000), Times.Once);
    }

    [Fact]
    public void SaveProgress_EmptyPath_Skips()
    {
        _mediaMock.Setup(m => m.CurrentFilePath).Returns((string?)null);

        _manager.SaveProgress();

        _settingsMock.Verify(s => s.SetVideoProgress(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task PlayNext_SetsIsPlayedOnPrevious()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        var firstItem = _manager.Items[0];

        _manager.PlayNext();

        firstItem.IsPlayed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateThumbnailReady_SetsFlag()
    {
        CreateVideoFiles(2);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        var item = _manager.Items[1];

        _manager.SyncThumbnailVisualStates(
            new Dictionary<string, ThumbnailActiveTaskSnapshot>(),
            path => string.Equals(path, item.FilePath, StringComparison.OrdinalIgnoreCase)
                ? ThumbnailState.Ready
                : ThumbnailState.Pending);

        item.IsThumbnailReady.Should().BeTrue();
        item.ThumbnailProgress.Should().Be(0);
    }

    [Fact]
    public async Task UpdateThumbnailProgress_SetsPercent()
    {
        CreateVideoFiles(2);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        var item = _manager.Items[0];

        _manager.SyncThumbnailVisualStates(
            new Dictionary<string, ThumbnailActiveTaskSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                [item.FilePath] = new ThumbnailActiveTaskSnapshot(
                    item.FilePath,
                    Path.GetFileName(item.FilePath),
                    ThumbnailWorkIntent.PlaybackCurrent,
                    ThumbnailState.Generating,
                    75,
                    true,
                    false)
            },
            _ => ThumbnailState.Pending);

        item.ThumbnailProgress.Should().Be(75);
        item.IsThumbnailReady.Should().BeFalse();
    }

    [Fact]
    public async Task SyncThumbnailVisualStates_CanceledOrQueued_HidesPie()
    {
        CreateVideoFiles(2);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        var item = _manager.Items[0];

        _manager.SyncThumbnailVisualStates(
            new Dictionary<string, ThumbnailActiveTaskSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                [item.FilePath] = new ThumbnailActiveTaskSnapshot(
                    item.FilePath,
                    Path.GetFileName(item.FilePath),
                    ThumbnailWorkIntent.PlaybackCurrent,
                    ThumbnailState.Generating,
                    42,
                    true,
                    false)
            },
            _ => ThumbnailState.Pending);

        _manager.SyncThumbnailVisualStates(
            new Dictionary<string, ThumbnailActiveTaskSnapshot>(),
            _ => ThumbnailState.Pending);

        item.ThumbnailProgress.Should().Be(0);
        item.IsThumbnailReady.Should().BeFalse();
    }

    [Fact]
    public async Task CurrentItem_ReturnsItemAtCurrentIndex()
    {
        CreateVideoFiles(3);
        await _manager.LoadFolderAsync(_tempDir, "Test");
        _manager.CurrentIndex = 1;

        _manager.CurrentItem!.Number.Should().Be(2);
    }

    [Fact]
    public async Task LoadFolder_SetsCurrentIndex()
    {
        CreateVideoFiles(3);

        await _manager.LoadFolderAsync(_tempDir, "Test");

        _manager.CurrentIndex.Should().Be(0);
        _manager.ItemCount.Should().Be(3);
        _manager.CurrentFolderPath.Should().Be(_tempDir);
    }

    [Fact]
    public async Task VideoPlayed_Event_FiresOnPlayEpisode()
    {
        CreateVideoFiles(2);
        string? playedPath = null;
        _manager.VideoPlayed += path => playedPath = path;

        await _manager.LoadFolderAsync(_tempDir, "Test");

        // PlayEpisode calls PlayCurrentVideo which fires VideoPlayed
        _manager.PlayEpisode(1);

        playedPath.Should().NotBeNull();
    }

    [Fact]
    public async Task PlayEpisode_WhenPlaybackFails_RaisesPlaybackFailedAndSkipsProgressWrites()
    {
        CreateVideoFiles(2);
        PlaybackFailureInfo? failure = null;
        _manager.PlaybackFailed += info => failure = info;
        _mediaMock
            .Setup(media => media.TryPlay(It.IsAny<string>(), It.IsAny<long>(), out It.Ref<string?>.IsAny))
            .Returns((string path, long _, out string? errorMessage) =>
            {
                errorMessage = "decoder failed";
                return false;
            });

        await _manager.LoadFolderAsync(_tempDir, "Test");

        _manager.PlayEpisode(1);

        failure.Should().NotBeNull();
        failure!.FilePath.Should().EndWith("ep02.mp4");
        failure.ErrorMessage.Should().Be("decoder failed");
        _settingsMock.Verify(settings => settings.SetFolderProgress(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _settingsMock.Verify(settings => settings.MarkVideoPlayed(It.IsAny<string>()), Times.Never);
    }

    private void CreateVideoFiles(int count)
    {
        for (int i = 1; i <= count; i++)
            File.WriteAllText(Path.Combine(_tempDir, $"ep{i:D2}.mp4"), "");
    }
}



