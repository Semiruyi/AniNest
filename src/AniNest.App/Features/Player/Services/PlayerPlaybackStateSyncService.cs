using System.Collections.Generic;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Features.Player.Playback;
using AniNest.Infrastructure.Presentation;

namespace AniNest.Features.Player.Services;

public sealed class PlayerPlaybackStateSyncService : IPlayerPlaybackStateSyncService
{
    private readonly PlayerSessionController _session;
    private readonly IPlaybackEngine _playbackEngine;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly Action<string> _videoPathChangedHandler;
    private readonly EventHandler _playingHandler;
    private readonly EventHandler _pausedHandler;
    private readonly EventHandler _stoppedHandler;
    private readonly EventHandler<PlaybackProgressChangedEventArgs> _progressChangedHandler;
    private PlayerPlaybackStateController? _controller;

    public PlayerPlaybackStateSyncService(
        PlayerSessionController session,
        IPlaybackEngine playbackEngine,
        IUiDispatcher uiDispatcher)
    {
        _session = session;
        _playbackEngine = playbackEngine;
        _uiDispatcher = uiDispatcher;
        _videoPathChangedHandler = OnSessionCurrentVideoPathChanged;
        _playingHandler = (_, _) => Dispatch("Playing", controller => controller.SetPlayingState(true));
        _pausedHandler = (_, _) => Dispatch("Paused", controller => controller.SetPlayingState(false));
        _stoppedHandler = (_, _) => Dispatch("Stopped", controller => controller.SetPlayingState(false));
        _progressChangedHandler = (_, args) => Dispatch("ProgressChanged", controller => controller.UpdateProgress(args), instrument: false);
    }

    public void Attach(PlayerPlaybackStateController controller)
    {
        if (ReferenceEquals(_controller, controller))
            return;

        if (_controller != null)
            Detach(_controller);

        _controller = controller;
        _session.CurrentVideoPathChanged += _videoPathChangedHandler;
        _playbackEngine.Playing += _playingHandler;
        _playbackEngine.Paused += _pausedHandler;
        _playbackEngine.Stopped += _stoppedHandler;
        _playbackEngine.ProgressChanged += _progressChangedHandler;
    }

    public void Detach(PlayerPlaybackStateController controller)
    {
        if (!ReferenceEquals(_controller, controller))
            return;

        _session.CurrentVideoPathChanged -= _videoPathChangedHandler;
        _playbackEngine.Playing -= _playingHandler;
        _playbackEngine.Paused -= _pausedHandler;
        _playbackEngine.Stopped -= _stoppedHandler;
        _playbackEngine.ProgressChanged -= _progressChangedHandler;
        _controller = null;
    }

    private void OnSessionCurrentVideoPathChanged(string path)
        => Dispatch("CurrentVideoPathChanged", controller => controller.SetCurrentVideoPath(path));

    private void Dispatch(string eventName, Action<PlayerPlaybackStateController> action, bool instrument = true)
    {
        if (_controller == null)
            return;

        var controller = _controller;
        var tags = instrument
            ? new Dictionary<string, string>
            {
                ["event"] = eventName
            }
            : null;

        if (_uiDispatcher.CheckAccess())
        {
            if (instrument)
            {
                using var executeSpan = PerfSpan.Begin("PlayerPlaybackStateSync.Execute", tags);
                action(controller);
            }
            else
            {
                action(controller);
            }

            return;
        }

        if (instrument)
        {
            using var waitSpan = PerfSpan.Begin("PlayerPlaybackStateSync.DispatchWait", tags);
            _uiDispatcher.Invoke(() =>
            {
                using var executeSpan = PerfSpan.Begin("PlayerPlaybackStateSync.Execute", tags);
                action(controller);
            });
        }
        else
        {
            _uiDispatcher.Invoke(() => action(controller));
        }
    }
}
