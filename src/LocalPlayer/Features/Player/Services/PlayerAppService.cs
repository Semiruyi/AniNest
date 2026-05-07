using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;

namespace LocalPlayer.Features.Player.Services;

public sealed class PlayerAppService : IPlayerAppService
{
    private static readonly Logger Log = AppLog.For<PlayerAppService>();
    private readonly ITaskbarAutoHideCoordinator _taskbarAutoHide;
    private readonly PlayerSessionController _session;
    private CancellationTokenSource? _loadCts;
    private long _loadGeneration;
    private long _loadedGeneration;
    private PerfSpan? _loadFolderSpan;

    public PlayerAppService(
        ITaskbarAutoHideCoordinator taskbarAutoHide,
        PlayerSessionController session)
    {
        _taskbarAutoHide = taskbarAutoHide;
        _session = session;
    }

    public async Task EnterPlayerAsync(string animationCode, string path, string name)
    {
        _ = _taskbarAutoHide.EnterPlayerPageAsync(animationCode);
        if (_session.IsCleanedUp)
            return;

        CancelAndDispose(ref _loadCts);
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;

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

        _session.ActivateCurrentVideo();
        _loadedGeneration = generation;
        Log.Info($"LoadFolderDataAsync complete: CurrentIndex={_session.CurrentIndex}, CurrentVideoPath={_session.CurrentVideoPath ?? "null"}");
    }

    public Task LeavePlayerAsync()
        => _taskbarAutoHide.LeavePlayerPageAsync();

    private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }
}
