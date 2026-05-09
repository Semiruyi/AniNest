using FluentAssertions;
using Moq;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.Model;

public class ThumbnailGeneratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _settingsService;
    private readonly Mock<IThumbnailDecodeStrategyService> _decodeStrategyService = new();
    private readonly ThumbnailGenerator _generator;

    public ThumbnailGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThumbnailGeneratorTests_{Guid.NewGuid():N}");
        var configDir = Path.Combine(_tempDir, "data", "config");
        Directory.CreateDirectory(configDir);

        _settingsService = new SettingsService(Path.Combine(configDir, "settings.json"));
        _decodeStrategyService.Setup(service => service.GetStrategyChain())
            .Returns(new[] { ThumbnailDecodeStrategy.Software });

        _generator = new ThumbnailGenerator(_settingsService, _decodeStrategyService.Object);
        WaitForInitialization();
    }

    public void Dispose()
    {
        _generator.Dispose();
        _settingsService.Dispose();

        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void EnqueueFolder_DuplicateVideos_DoesNotDuplicateTasks()
    {
        var videos = new[] { @"C:\videos\a.mp4", @"C:\videos\b.mp4" };

        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);
        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);

        var matchingPaths = _generator.GetTaskVideoPathsInOrder()
            .Where(videos.Contains)
            .ToArray();

        matchingPaths.Should().HaveCount(2);
        matchingPaths
            .Should()
            .Equal(videos);
    }

    [Fact]
    public void EnqueueFolder_OrdersLastPlayedBeforeUnplayedBeforePlayed()
    {
        var lastPlayed = @"C:\videos\ep03.mp4";
        var unplayed = @"C:\videos\ep02.mp4";
        var played = @"C:\videos\ep01.mp4";

        _settingsService.MarkVideoPlayed(lastPlayed);
        _settingsService.MarkVideoPlayed(played);

        _generator.EnqueueFolder(
            @"C:\videos",
            [played, unplayed, lastPlayed],
            cardOrder: 2,
            lastPlayedPath: lastPlayed,
            playedPaths: [lastPlayed, played]);

        _generator.GetTaskVideoPathsInOrder()
            .Where(path => path is not null && (path == lastPlayed || path == unplayed || path == played))
            .Should()
            .Equal(lastPlayed, unplayed, played);
    }

    [Fact]
    public void RequeueActiveWorkers_WhenNoActiveWorkers_DoesNotChangePendingTaskState()
    {
        var video = @"C:\videos\ep01.mp4";
        _generator.EnqueueFolder(@"C:\videos", [video], 0, null, []);
        _generator.ForceTaskState(video, ThumbnailState.Pending);

        _generator.RequeueActiveWorkersForTest("test-requeue");

        _generator.GetState(video).Should().Be(ThumbnailState.Pending);
    }

    [Fact]
    public void TryRequeueTask_WhenGenerating_RevertsToPendingWithoutChangingReadyCount()
    {
        var video = @"C:\videos\ep01.mp4";
        _generator.EnqueueFolder(@"C:\videos", [video], 0, null, []);
        _generator.ForceTaskState(video, ThumbnailState.Generating);

        var requeued = _generator.TryRequeueTaskForTest(video);

        requeued.Should().BeTrue();
        _generator.GetState(video).Should().Be(ThumbnailState.Pending);
        _generator.GetStatusSnapshot().ReadyCount.Should().Be(0);
        _generator.CountTasksByState(ThumbnailState.Pending).Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryRequeueTask_WhenReady_DoesNothingAndKeepsReadyCount()
    {
        var video = @"C:\videos\ep02.mp4";
        _generator.EnqueueFolder(@"C:\videos", [video], 0, null, []);
        _generator.ForceTaskState(video, ThumbnailState.Ready);
        var readyBefore = _generator.GetStatusSnapshot().ReadyCount;

        var requeued = _generator.TryRequeueTaskForTest(video);

        requeued.Should().BeFalse();
        _generator.GetState(video).Should().Be(ThumbnailState.Ready);
        _generator.GetStatusSnapshot().ReadyCount.Should().Be(readyBefore);
    }

    [Fact]
    public void ForceTaskState_ReadyToPending_UpdatesReadyCount()
    {
        var video = @"C:\videos\ep03.mp4";
        _generator.EnqueueFolder(@"C:\videos", [video], 0, null, []);
        _generator.ForceTaskState(video, ThumbnailState.Ready);
        _generator.GetStatusSnapshot().ReadyCount.Should().BeGreaterThan(0);

        _generator.ForceTaskState(video, ThumbnailState.Pending);

        _generator.GetState(video).Should().Be(ThumbnailState.Pending);
        _generator.GetStatusSnapshot().ReadyCount.Should().Be(0);
    }

    private void WaitForInitialization()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_generator.IsFfmpegAvailable && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }
    }
}
