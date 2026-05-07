using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Media;
using System.Windows;
using System.Windows.Threading;

namespace LocalPlayer.Features.Player.Services;

public sealed class PlayerAppService : IPlayerAppService
{
    private static readonly Logger Log = AppLog.For<PlayerAppService>();
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly PlayerSessionController _session;
    private readonly PlayerPlaybackStateController _playback;
    private readonly IMediaPlayerController _media;
    private CancellationTokenSource? _loadCts;
    private long _loadGeneration;
    private long _loadedGeneration;
    private long _activatedGeneration;
    private long _pendingActivationGeneration;
    private PerfSpan? _loadFolderSpan;
    private bool _isPlayerPageVisible;

    public PlayerAppService(
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        PlayerSessionController session,
        PlayerPlaybackStateController playback,
        IMediaPlayerController media)
    {
        _taskbarAutoHide = taskbarAutoHide;
        _session = session;
        _playback = playback;
        _media = media;
    }

    public async Task EnterPlayerAsync(string animationCode, string path, string name)
    {
        _ = _taskbarAutoHide.EnterPlayerPageAsync(animationCode);
        if (_session.IsCleanedUp)
            return;

        CancelAndDispose(ref _loadCts);
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;
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
        _pendingActivationGeneration = generation;
        TryActivatePendingVideo("data-loaded");
        Log.Info($"LoadFolderDataAsync complete: CurrentIndex={_session.CurrentIndex}, CurrentVideoPath={_session.CurrentVideoPath ?? "null"}");
    }

    public Task LeavePlayerAsync()
    {
        _isPlayerPageVisible = false;
        _pendingActivationGeneration = 0;
        _activatedGeneration = 0;
        CancelAndDispose(ref _loadCts);
        _media.ResetSession();
        _session.ResetSession();
        _playback.ResetSession();
        return _taskbarAutoHide.LeavePlayerPageAsync();
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

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            ActivatePendingVideo(generation, reason);
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            ActivatePendingVideo(generation, $"{reason}-dispatch");
        }));
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
        Log.Info($"ActivateCurrentVideo complete: generation={generation}, index={_session.CurrentIndex}, path={_session.CurrentVideoPath ?? "null"}");
    }
}
