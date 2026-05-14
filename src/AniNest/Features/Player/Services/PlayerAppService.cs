using AniNest.Infrastructure.Interop;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Presentation;
using AniNest.Infrastructure.Thumbnails;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace AniNest.Features.Player.Services;

public sealed class PlayerAppService : IPlayerAppService
{
    private static readonly Logger Log = AppLog.For<PlayerAppService>();
    private const double FrameBudgetMs160Hz = 1000.0 / 160.0;
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly PlayerSessionController _session;
    private readonly PlayerPlaybackStateController _playback;
    private readonly IMediaPlayerController _media;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IUiDispatcher _uiDispatcher;
    private CancellationTokenSource? _loadCts;
    private long _loadGeneration;
    private long _loadedGeneration;
    private long _activatedGeneration;
    private long _pendingActivationGeneration;
    private PerfSpan? _loadFolderSpan;
    private bool _isPlayerPageVisible;
    private bool _isLeavingPlayer;

    public PlayerAppService(
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        PlayerSessionController session,
        PlayerPlaybackStateController playback,
        IMediaPlayerController media,
        IThumbnailGenerator thumbnailGenerator,
        IUiDispatcher uiDispatcher)
    {
        _taskbarAutoHide = taskbarAutoHide;
        _session = session;
        _playback = playback;
        _media = media;
        _thumbnailGenerator = thumbnailGenerator;
        _uiDispatcher = uiDispatcher;
        _session.CurrentIndexChanged += OnSessionCurrentIndexChanged;
    }

    public async Task EnterPlayerAsync(string animationCode, string path, string name)
    {
        if (_isLeavingPlayer)
        {
            Log.Warning(
                $"EnterPlayer requested while leave transition is still in progress: folder={name}, path={path}, " +
                $"loadGeneration={_loadGeneration}, loadedGeneration={_loadedGeneration}, activatedGeneration={_activatedGeneration}");
        }

        _thumbnailGenerator.SetPlayerActive(true);
        _ = _taskbarAutoHide.EnterPlayerPageAsync(animationCode);
        if (_session.IsCleanedUp)
            return;

        Log.Info(MemorySnapshot.Capture("PlayerAppService.EnterPlayer.begin",
            ("folder", name),
            ("loadGeneration", _loadGeneration),
            ("loadedGeneration", _loadedGeneration),
            ("activatedGeneration", _activatedGeneration),
            ("items", _session.PlaylistItems.Count)));

        CancelAndDispose(ref _loadCts);
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;
        _isLeavingPlayer = false;
        _isPlayerPageVisible = false;
        _pendingActivationGeneration = 0;

        Log.Info($"LoadFolderSkeleton start: {name} | {path}");
        _loadGeneration++;
        _loadFolderSpan?.Dispose();
        _loadFolderSpan = PerfSpan.Begin("Player.LoadFolderSkeleton", new Dictionary<string, string>
        {
            ["folder"] = name
        });

        using (PerfSpan.Begin("Player.Playlist.LoadFolderSkeleton", new Dictionary<string, string>
        {
            ["folder"] = name
        }))
        {
            await _session.LoadFolderSkeletonAsync(path, name, cancellationToken);
        }

        Log.Info($"LoadFolderSkeleton done: generation={_loadGeneration}, items={_session.PlaylistItems.Count}");
        _loadFolderSpan?.Dispose();
        _loadFolderSpan = null;

        var generation = _loadGeneration;
        if (generation == _loadedGeneration)
        {
            Log.Debug($"EnterPlayerAsync skipped data load: generation already loaded ({generation})");
            return;
        }

        Log.Info($"LoadFolderDataAsync start: generation={generation}, loaded={_loadedGeneration}, items={_session.PlaylistItems.Count}");
        using (PerfSpan.Begin("Player.LoadFolderData"))
        {
            await _session.LoadFolderDataAsync(cancellationToken);
        }

        if (_session.IsCleanedUp || generation != _loadGeneration)
            return;

        _loadedGeneration = generation;
        RefreshPlaybackThumbnailWindow();
        _pendingActivationGeneration = generation;
        TryActivatePendingVideo("data-loaded");
        Log.Info($"LoadFolderDataAsync complete: CurrentIndex={_session.CurrentIndex}, CurrentItemPath={_session.CurrentItem?.FilePath ?? "null"}, CurrentVideoPath={_session.CurrentVideoPath ?? "null"}");
        Log.Info(MemorySnapshot.Capture("PlayerAppService.EnterPlayer.end",
            ("folder", name),
            ("generation", generation),
            ("currentIndex", _session.CurrentIndex),
            ("items", _session.PlaylistItems.Count)));
    }

    public Task BeginLeavePlayerAsync()
    {
        if (_isLeavingPlayer)
        {
            Log.Warning(
                $"BeginLeavePlayer ignored: already leaving. loadGeneration={_loadGeneration}, loadedGeneration={_loadedGeneration}, " +
                $"activatedGeneration={_activatedGeneration}, pendingGeneration={_pendingActivationGeneration}");
            return Task.CompletedTask;
        }

        _thumbnailGenerator.SetPlayerActive(false);
        Log.Info(
            $"BeginLeavePlayer: loadGeneration={_loadGeneration}, loadedGeneration={_loadedGeneration}, " +
            $"activatedGeneration={_activatedGeneration}, pendingGeneration={_pendingActivationGeneration}, " +
            $"items={_session.PlaylistItems.Count}, currentIndex={_session.CurrentIndex}, pageVisible={_isPlayerPageVisible}");
        _isLeavingPlayer = true;
        _isPlayerPageVisible = false;
        _pendingActivationGeneration = 0;
        _activatedGeneration = 0;
        CancelAndDispose(ref _loadCts);
        Log.Info(
            $"BeginLeavePlayer done: loadGeneration={_loadGeneration}, loadedGeneration={_loadedGeneration}, " +
            $"activatedGeneration={_activatedGeneration}, pendingGeneration={_pendingActivationGeneration}, " +
            $"items={_session.PlaylistItems.Count}, currentIndex={_session.CurrentIndex}, " +
            $"pageVisible={_isPlayerPageVisible}, isLeavingPlayer={_isLeavingPlayer}");
        return _taskbarAutoHide.LeavePlayerPageAsync();
    }

    public void CompleteLeavePlayerTransition()
    {
        if (!_isLeavingPlayer)
        {
            Log.Debug("CompleteLeavePlayerTransition skipped: player is not leaving");
            return;
        }

        var totalStopwatch = Stopwatch.StartNew();

        Log.Info(
            $"CompleteLeavePlayerTransition begin: loadGeneration={_loadGeneration}, loadedGeneration={_loadedGeneration}, " +
            $"activatedGeneration={_activatedGeneration}, pendingGeneration={_pendingActivationGeneration}, " +
            $"items={_session.PlaylistItems.Count}, currentIndex={_session.CurrentIndex}, " +
            $"pageVisible={_isPlayerPageVisible}, isLeavingPlayer={_isLeavingPlayer}");

        var resetStopwatch = Stopwatch.StartNew();
        _playback.ResetSession();
        resetStopwatch.Stop();
        _isLeavingPlayer = false;
        totalStopwatch.Stop();

        Log.Info(
            $"CompleteLeavePlayerTransition end: loadGeneration={_loadGeneration}, loadedGeneration={_loadedGeneration}, " +
            $"activatedGeneration={_activatedGeneration}, pendingGeneration={_pendingActivationGeneration}, " +
            $"items={_session.PlaylistItems.Count}, currentIndex={_session.CurrentIndex}, " +
            $"pageVisible={_isPlayerPageVisible}, isLeavingPlayer={_isLeavingPlayer}");

        Log.Info(
            $"CompleteLeavePlayerTransition timing: playbackReset={resetStopwatch.Elapsed.TotalMilliseconds:F3}ms, " +
            $"total={totalStopwatch.Elapsed.TotalMilliseconds:F3}ms, " +
            $"budget={FrameBudgetMs160Hz:F2}ms @160Hz, overBudget={totalStopwatch.Elapsed.TotalMilliseconds > FrameBudgetMs160Hz}");
    }

    public void OnPlayerPageTransitionCompleted()
    {
        _isPlayerPageVisible = true;
        Log.Info($"Player page transition completed: pending={_pendingActivationGeneration}, loaded={_loadedGeneration}, activated={_activatedGeneration}");
        TryActivatePendingVideo("transition-complete");
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    private void TryActivatePendingVideo(string reason)
    {
        var generation = _pendingActivationGeneration;
        if (generation <= 0)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): no pending generation");
            return;
        }

        if (!_isPlayerPageVisible)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): player page not visible yet");
            return;
        }

        if (generation != _loadGeneration || generation != _loadedGeneration)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): generation mismatch pending={generation}, load={_loadGeneration}, loaded={_loadedGeneration}");
            return;
        }

        if (generation == _activatedGeneration)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): generation {generation} already activated");
            return;
        }

        if (_uiDispatcher.CheckAccess())
        {
            ActivatePendingVideo(generation, reason);
            return;
        }

        _uiDispatcher.BeginInvoke(() =>
        {
            ActivatePendingVideo(generation, $"{reason}-dispatch");
        });
    }

    private void ActivatePendingVideo(long generation, string reason)
    {
        if (_session.IsCleanedUp)
            return;

        if (!_isPlayerPageVisible)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): player page no longer visible");
            return;
        }

        if (generation != _pendingActivationGeneration || generation != _loadGeneration || generation != _loadedGeneration)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): generation changed pending={_pendingActivationGeneration}, load={_loadGeneration}, loaded={_loadedGeneration}, requested={generation}");
            return;
        }

        if (generation == _activatedGeneration)
        {
            Log.Debug($"Skip ActivateCurrentVideo ({reason}): generation {generation} already activated");
            return;
        }

        Log.Info($"ActivateCurrentVideo start: generation={generation}, reason={reason}");
        using (PerfSpan.Begin("Player.ActivateCurrentVideo", new Dictionary<string, string>
        {
            ["reason"] = reason,
            ["generation"] = generation.ToString()
        }))
        {
            _session.ActivateCurrentVideo();
        }

        _activatedGeneration = generation;
        Log.Info($"ActivateCurrentVideo complete: generation={generation}, index={_session.CurrentIndex}, itemPath={_session.CurrentItem?.FilePath ?? "null"}, path={_session.CurrentVideoPath ?? "null"}");
    }

    private void OnSessionCurrentIndexChanged(int _)
    {
        if (_isLeavingPlayer || !_isPlayerPageVisible)
        {
            Log.Debug(
                $"Skip RefreshPlaybackThumbnailWindow on index change: leaving={_isLeavingPlayer}, " +
                $"pageVisible={_isPlayerPageVisible}, currentIndex={_session.CurrentIndex}, " +
                $"currentVideoPath={_session.CurrentVideoPath ?? "null"}");
            return;
        }

        Log.Info(
            $"Player current index changed: currentIndex={_session.CurrentIndex}, " +
            $"currentItemPath={_session.CurrentItem?.FilePath ?? "null"}, currentVideoPath={_session.CurrentVideoPath ?? "null"}, items={_session.PlaylistItems.Count}");
        RefreshPlaybackThumbnailWindow();
    }

    private void RefreshPlaybackThumbnailWindow()
    {
        if (_session.CurrentIndex < 0 || _session.PlaylistItems.Count == 0)
        {
            Log.Debug(
                $"Skip RefreshPlaybackThumbnailWindow: currentIndex={_session.CurrentIndex}, " +
                $"items={_session.PlaylistItems.Count}, currentVideoPath={_session.CurrentVideoPath ?? "null"}");
            return;
        }

        string[] orderedVideoPaths = _session.PlaylistItems.Select(item => item.FilePath).ToArray();
        int currentIndex = _session.CurrentIndex;
        int start = Math.Max(0, currentIndex - 1);
        int end = Math.Min(orderedVideoPaths.Length - 1, currentIndex + 3);
        string windowSummary = string.Join(", ",
            Enumerable.Range(start, end - start + 1)
                .Select(i => $"{i}:{Path.GetFileName(orderedVideoPaths[i])}{(i == currentIndex ? "*" : "")}"));

        Log.Info(
            $"RefreshPlaybackThumbnailWindow: currentIndex={currentIndex}, currentItemPath={_session.CurrentItem?.FilePath ?? "null"}, currentVideoPath={_session.CurrentVideoPath ?? "null"}, " +
            $"rawWindow=[{windowSummary}], items={orderedVideoPaths.Length}");

        _thumbnailGenerator.BoostPlaybackWindow(
            orderedVideoPaths,
            currentIndex,
            3);
    }
}
