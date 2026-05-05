using System.IO;
using FluentAssertions;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using Moq;
using Xunit;

namespace LocalPlayer.Tests.Model;

public class PlaylistManagerTests : IDisposable
{
    private readonly Mock<ISettingsService> _settingsMock = new();
    private readonly Mock<IMediaPlayerController> _mediaMock = new();
    private readonly PlaylistManager _manager;
    private readonly string _tempDir;

    public PlaylistManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PlaylistManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _manager = new PlaylistManager(
            _settingsMock.Object,
            _mediaMock.Object,
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
    public void PlayNext_AtStart_AdvancesIndex()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");

        var result = _manager.PlayNext();

        result.Should().BeTrue();
        _manager.CurrentIndex.Should().Be(1);
    }

    [Fact]
    public void PlayNext_AtEnd_ReturnsFalse()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");
        // Move to last
        _manager.CurrentIndex = 2;

        var result = _manager.PlayNext();

        result.Should().BeFalse();
        _manager.CurrentIndex.Should().Be(2);
    }

    [Fact]
    public void PlayPrevious_AtStart_ReturnsFalse()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");

        var result = _manager.PlayPrevious();

        result.Should().BeFalse();
        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void PlayPrevious_AtMiddle_GoesBack()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");
        _manager.CurrentIndex = 1;

        var result = _manager.PlayPrevious();

        result.Should().BeTrue();
        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void PlayEpisode_ValidIndex_PlaysIt()
    {
        CreateVideoFiles(5);
        _manager.LoadFolder(_tempDir, "Test");

        _manager.PlayEpisode(3);

        _manager.CurrentIndex.Should().Be(3);
    }

    [Fact]
    public void PlayEpisode_OutOfRange_DoesNothing()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");

        _manager.PlayEpisode(10);

        _manager.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void PlayEpisode_NegativeIndex_DoesNothing()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");

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
    public void PlayNext_SetsIsPlayedOnPrevious()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");
        var firstItem = _manager.Items[0];

        _manager.PlayNext();

        firstItem.IsPlayed.Should().BeTrue();
    }

    [Fact]
    public void UpdateThumbnailReady_SetsFlag()
    {
        CreateVideoFiles(2);
        _manager.LoadFolder(_tempDir, "Test");
        var item = _manager.Items[1];

        _manager.UpdateThumbnailReady(item.FilePath);

        item.IsThumbnailReady.Should().BeTrue();
    }

    [Fact]
    public void UpdateThumbnailProgress_SetsPercent()
    {
        CreateVideoFiles(2);
        _manager.LoadFolder(_tempDir, "Test");
        var item = _manager.Items[0];

        _manager.UpdateThumbnailProgress(item.FilePath, 75);

        item.ThumbnailProgress.Should().Be(75);
    }

    [Fact]
    public void CurrentItem_ReturnsItemAtCurrentIndex()
    {
        CreateVideoFiles(3);
        _manager.LoadFolder(_tempDir, "Test");
        _manager.CurrentIndex = 1;

        _manager.CurrentItem!.Number.Should().Be(2);
    }

    [Fact]
    public void LoadFolder_SetsCurrentIndex()
    {
        CreateVideoFiles(3);

        _manager.LoadFolder(_tempDir, "Test");

        _manager.CurrentIndex.Should().Be(0);
        _manager.ItemCount.Should().Be(3);
        _manager.CurrentFolderPath.Should().Be(_tempDir);
    }

    [Fact]
    public void VideoPlayed_Event_FiresOnPlayEpisode()
    {
        CreateVideoFiles(2);
        string? playedPath = null;
        _manager.VideoPlayed += path => playedPath = path;

        _manager.LoadFolder(_tempDir, "Test");

        // PlayEpisode calls PlayCurrentVideo which fires VideoPlayed
        _manager.PlayEpisode(1);

        playedPath.Should().NotBeNull();
    }

    private void CreateVideoFiles(int count)
    {
        for (int i = 1; i <= count; i++)
            File.WriteAllText(Path.Combine(_tempDir, $"ep{i:D2}.mp4"), "");
    }
}



