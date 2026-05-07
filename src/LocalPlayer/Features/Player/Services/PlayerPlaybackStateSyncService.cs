using System.Windows;
using LocalPlayer.Infrastructure.Media;

namespace LocalPlayer.Features.Player.Services;

public sealed class PlayerPlaybackStateSyncService : IPlayerPlaybackStateSyncService
{
    private readonly PlayerSessionController _session;
    private readonly IMediaPlayerController _media;
    private readonly Action<string> _videoPathChangedHandler;
    private readonly EventHandler _playingHandler;
    private readonly EventHandler _pausedHandler;
    private readonly EventHandler _stoppedHandler;
    private readonly EventHandler<ProgressUpdatedEventArgs> _progressUpdatedHandler;
    private PlayerPlaybackStateController? _controller;

    public PlayerPlaybackStateSyncService(
        PlayerSessionController session,
        IMediaPlayerController media)
    {
        _session = session;
        _media = media;
        _videoPathChangedHandler = OnSessionCurrentVideoPathChanged;
        _playingHandler = (_, _) => Dispatch(controller => controller.SetPlayingState(true));
        _pausedHandler = (_, _) => Dispatch(controller => controller.SetPlayingState(false));
        _stoppedHandler = (_, _) => Dispatch(controller =>
        {
            controller.SetPlayingState(false);
            controller.RefreshVideoSource();
        });
        _progressUpdatedHandler = (_, args) => Dispatch(controller => controller.UpdateProgress(args));
    }

    public void Attach(PlayerPlaybackStateController controller)
    {
        if (ReferenceEquals(_controller, controller))
            return;

        if (_controller != null)
            Detach(_controller);

        _controller = controller;
        _session.CurrentVideoPathChanged += _videoPathChangedHandler;
        _media.Playing += _playingHandler;
        _media.Paused += _pausedHandler;
        _media.Stopped += _stoppedHandler;
        _media.ProgressUpdated += _progressUpdatedHandler;
    }

    public void Detach(PlayerPlaybackStateController controller)
    {
        if (!ReferenceEquals(_controller, controller))
            return;

        _session.CurrentVideoPathChanged -= _videoPathChangedHandler;
        _media.Playing -= _playingHandler;
        _media.Paused -= _pausedHandler;
        _media.Stopped -= _stoppedHandler;
        _media.ProgressUpdated -= _progressUpdatedHandler;
        _controller = null;
    }

    private void OnSessionCurrentVideoPathChanged(string path)
        => Dispatch(controller => controller.SetCurrentVideoPath(path));

    private void Dispatch(Action<PlayerPlaybackStateController> action)
    {
        if (_controller == null)
            return;

        Application.Current.Dispatcher.Invoke(() => action(_controller));
    }
}
