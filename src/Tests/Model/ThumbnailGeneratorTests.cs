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
        matchingPaths.Should().Equal(videos);
    }

    [Fact]
    public void EnqueueFolder_SeedsVideosAsBackgroundFill()
    {
        var videos = new[] { @"C:\videos\ep01.mp4", @"C:\videos\ep02.mp4" };

        _generator.EnqueueFolder(@"C:\videos", videos, 2, videos[0], [videos[0]]);

        _generator.GetIntent(videos[0]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
        _generator.GetIntent(videos[1]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
    }

    [Fact]
    public void FocusCollection_PromotesCollectionVideosAboveBackground()
    {
        var focusVideos = new[] { @"C:\videos\a.mp4", @"C:\videos\b.mp4" };
        var backgroundVideo = @"C:\other\c.mp4";

        _generator.RegisterCollection(
            new LibraryCollectionRef("folder:focus", LibraryCollectionKind.Folder, "Focus"),
            focusVideos);
        _generator.RegisterCollection(
            new LibraryCollectionRef("folder:other", LibraryCollectionKind.Folder, "Other"),
            [backgroundVideo]);

        _generator.FocusCollection("folder:focus");

        _generator.GetIntent(focusVideos[0]).Should().Be(ThumbnailWorkIntent.FocusedCollection);
        _generator.GetIntent(focusVideos[1]).Should().Be(ThumbnailWorkIntent.FocusedCollection);
        _generator.GetIntent(backgroundVideo).Should().Be(ThumbnailWorkIntent.BackgroundFill);

        _generator.GetTaskVideoPathsInOrder().Take(2).Should().Equal(focusVideos);
    }

    [Fact]
    public void BoostVideo_PromotesSingleVideoAboveFocusedCollection()
    {
        var focusVideos = new[] { @"C:\videos\a.mp4", @"C:\videos\b.mp4" };

        _generator.RegisterCollection(
            new LibraryCollectionRef("folder:focus", LibraryCollectionKind.Folder, "Focus"),
            focusVideos);
        _generator.FocusCollection("folder:focus");

        _generator.BoostVideo(focusVideos[1]);

        _generator.GetIntent(focusVideos[0]).Should().Be(ThumbnailWorkIntent.FocusedCollection);
        _generator.GetIntent(focusVideos[1]).Should().Be(ThumbnailWorkIntent.ManualSingle);
        _generator.GetTaskVideoPathsInOrder().First().Should().Be(focusVideos[1]);
    }

    [Fact]
    public void BoostPlaybackWindow_PromotesCurrentAndNearbyVideos()
    {
        var videos = new[]
        {
            @"C:\videos\ep01.mp4",
            @"C:\videos\ep02.mp4",
            @"C:\videos\ep03.mp4",
            @"C:\videos\ep04.mp4"
        };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);
        _generator.BoostPlaybackWindow(videos, currentIndex: 1, lookaheadCount: 2);

        _generator.GetIntent(videos[1]).Should().Be(ThumbnailWorkIntent.PlaybackCurrent);
        _generator.GetIntent(videos[0]).Should().Be(ThumbnailWorkIntent.PlaybackNearby);
        _generator.GetIntent(videos[2]).Should().Be(ThumbnailWorkIntent.PlaybackNearby);
        _generator.GetIntent(videos[3]).Should().Be(ThumbnailWorkIntent.PlaybackNearby);
        _generator.GetStatusSnapshot().CurrentTargetName.Should().Be("ep02.mp4");
        _generator.GetStatusSnapshot().CurrentTargetIntent.Should().Be(nameof(ThumbnailWorkIntent.PlaybackCurrent));
    }

    [Fact]
    public void SetPlayerActive_False_DemotesPlaybackIntentsAndClearsPlaybackTarget()
    {
        var videos = new[]
        {
            @"C:\videos\ep01.mp4",
            @"C:\videos\ep02.mp4",
            @"C:\videos\ep03.mp4"
        };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);
        _generator.BoostPlaybackWindow(videos, currentIndex: 1, lookaheadCount: 1);

        _generator.SetPlayerActive(false);

        _generator.GetIntent(videos[0]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
        _generator.GetIntent(videos[1]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
        _generator.GetIntent(videos[2]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
        _generator.GetStatusSnapshot().CurrentTargetName.Should().BeNull();
        _generator.GetStatusSnapshot().CurrentTargetIntent.Should().BeNull();
    }

    [Fact]
    public void BoostPlaybackWindow_NewCurrent_PreemptsOldPlaybackWorker()
    {
        var videos = new[]
        {
            @"C:\videos\ep01.mp4",
            @"C:\videos\ep02.mp4",
            @"C:\videos\ep03.mp4"
        };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);
        _generator.BoostPlaybackWindow(videos, currentIndex: 0, lookaheadCount: 1);
        _generator.AddActiveWorkerForTest(videos[0]);

        _generator.BoostPlaybackWindow(videos, currentIndex: 2, lookaheadCount: 0);

        _generator.IsActiveWorkerCancellationRequestedForTest(videos[0]).Should().BeTrue();
        _generator.GetIntent(videos[2]).Should().Be(ThumbnailWorkIntent.PlaybackCurrent);
        _generator.GetStatusSnapshot().CurrentTargetName.Should().Be("ep03.mp4");
    }

    [Fact]
    public void BoostPlaybackWindow_NewCurrent_KeepsPreviousFirstNearbyPlaybackWorker()
    {
        var videos = new[]
        {
            @"C:\videos\ep01.mp4",
            @"C:\videos\ep02.mp4",
            @"C:\videos\ep03.mp4"
        };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);
        _generator.BoostPlaybackWindow(videos, currentIndex: 0, lookaheadCount: 1);
        _generator.AddActiveWorkerForTest(videos[0]);

        _generator.BoostPlaybackWindow(videos, currentIndex: 1, lookaheadCount: 1);

        _generator.IsActiveWorkerCancellationRequestedForTest(videos[0]).Should().BeFalse();
        _generator.IsActiveWorkerCancellationRequestedForTest(videos[1]).Should().BeFalse();
    }

    [Fact]
    public void BoostPlaybackWindow_SkipsReadyVideosWhenChoosingPlaybackCandidates()
    {
        var videos = new[]
        {
            @"C:\videos\ep01.mp4",
            @"C:\videos\ep02.mp4",
            @"C:\videos\ep03.mp4",
            @"C:\videos\ep04.mp4"
        };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.EnqueueFolder(@"C:\videos", videos, 0, null, []);
        _generator.ForceTaskState(videos[0], ThumbnailState.Ready);
        _generator.ForceTaskState(videos[1], ThumbnailState.Ready);
        _generator.ForceTaskState(videos[2], ThumbnailState.Ready);
        _generator.BoostPlaybackWindow(videos, currentIndex: 3, lookaheadCount: 0);
        _generator.AddActiveWorkerForTest(videos[3]);

        _generator.BoostPlaybackWindow(videos, currentIndex: 0, lookaheadCount: 3);

        _generator.IsActiveWorkerCancellationRequestedForTest(videos[3]).Should().BeFalse();
        _generator.GetIntent(videos[3]).Should().Be(ThumbnailWorkIntent.PlaybackCurrent);
        _generator.GetStatusSnapshot().CurrentTargetName.Should().Be("ep01.mp4");
    }

    [Fact]
    public void ResetCollection_WithBoostAfterReset_RequeuesCollectionAsManualCollection()
    {
        var videos = new[] { @"C:\videos\ep01.mp4", @"C:\videos\ep02.mp4" };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.RegisterCollection(new LibraryCollectionRef("folder:reset", LibraryCollectionKind.Folder, "Reset"), videos);
        _generator.ForceTaskState(videos[0], ThumbnailState.Ready);
        _generator.ResetCollection("folder:reset", boostAfterReset: true);

        _generator.GetState(videos[0]).Should().Be(ThumbnailState.Pending);
        _generator.GetIntent(videos[0]).Should().Be(ThumbnailWorkIntent.ManualCollection);
        _generator.GetIntent(videos[1]).Should().Be(ThumbnailWorkIntent.ManualCollection);
        _generator.GetStatusSnapshot().CurrentTargetIntent.Should().Be(nameof(ThumbnailWorkIntent.ManualCollection));
    }

    [Fact]
    public void ResetCollection_WithoutBoostAfterReset_RequeuesCollectionAsBackgroundFill()
    {
        var videos = new[] { @"C:\videos\ep01.mp4", @"C:\videos\ep02.mp4" };

        _settingsService.SetThumbnailGenerationPaused(true);
        _generator.RefreshGenerationPaused();
        _generator.RegisterCollection(new LibraryCollectionRef("folder:clear", LibraryCollectionKind.Folder, "Clear"), videos);
        _generator.BoostCollection("folder:clear");
        _generator.ResetCollection("folder:clear", boostAfterReset: false);

        _generator.GetIntent(videos[0]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
        _generator.GetIntent(videos[1]).Should().Be(ThumbnailWorkIntent.BackgroundFill);
    }

    [Fact]
    public void PreemptLowerPriorityWorkers_WhenManualSingleArrives_RequeuesBackgroundWorker()
    {
        var backgroundVideo = @"C:\videos\ep01.mp4";
        var boostedVideo = @"C:\videos\ep02.mp4";

        _generator.EnqueueFolder(@"C:\videos", [backgroundVideo, boostedVideo], 0, null, []);
        _generator.AddActiveWorkerForTest(backgroundVideo);

        _generator.BoostVideo(boostedVideo);
        _generator.PreemptLowerPriorityWorkersForTest(ThumbnailWorkIntent.ManualSingle);
        _generator.SimulateCanceledActiveWorkerForTest(backgroundVideo);

        _generator.GetState(backgroundVideo).Should().Be(ThumbnailState.Pending);
        _generator.GetIntent(backgroundVideo).Should().Be(ThumbnailWorkIntent.BackgroundFill);
        _generator.GetIntent(boostedVideo).Should().Be(ThumbnailWorkIntent.ManualSingle);
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
        _generator.BoostVideo(video);
        _generator.ForceTaskState(video, ThumbnailState.Generating);

        var requeued = _generator.TryRequeueTaskForTest(video);

        requeued.Should().BeTrue();
        _generator.GetState(video).Should().Be(ThumbnailState.Pending);
        _generator.GetStatusSnapshot().ReadyCount.Should().Be(0);
        _generator.CountTasksByState(ThumbnailState.Pending).Should().BeGreaterThan(0);
        _generator.GetIntent(video).Should().Be(ThumbnailWorkIntent.ManualSingle);
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
