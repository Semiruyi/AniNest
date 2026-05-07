using System.IO;
using System.Windows;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Thumbnails;

namespace LocalPlayer.Features.Player.Services;

public sealed class PlayerThumbnailSyncService : IPlayerThumbnailSyncService
{
    private static readonly Logger Log = AppLog.For<PlayerThumbnailSyncService>();

    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly Action<string> _videoReadyHandler;
    private readonly Action<string, int> _videoProgressHandler;
    private PlaylistViewModel? _playlist;

    public PlayerThumbnailSyncService(IThumbnailGenerator thumbnailGenerator)
    {
        _thumbnailGenerator = thumbnailGenerator;
        _videoReadyHandler = OnVideoReady;
        _videoProgressHandler = OnVideoProgress;
    }

    public void Attach(PlaylistViewModel playlist)
    {
        if (ReferenceEquals(_playlist, playlist))
            return;

        if (_playlist != null)
            Detach(_playlist);

        _playlist = playlist;
        _thumbnailGenerator.VideoReady += _videoReadyHandler;
        _thumbnailGenerator.VideoProgress += _videoProgressHandler;
    }

    public void Detach(PlaylistViewModel playlist)
    {
        if (!ReferenceEquals(_playlist, playlist))
            return;

        _thumbnailGenerator.VideoReady -= _videoReadyHandler;
        _thumbnailGenerator.VideoProgress -= _videoProgressHandler;
        _playlist = null;
    }

    private void OnVideoReady(string path)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_playlist == null)
            {
                Log.Debug($"VideoReady skipped (_playlist=null): {Path.GetFileName(path)}");
                return;
            }

            Log.Debug($"VideoReady -> UpdateThumbnailReady: {Path.GetFileName(path)}");
            _playlist.UpdateThumbnailReady(path);
        });
    }

    private void OnVideoProgress(string path, int percent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_playlist == null)
            {
                Log.Debug($"VideoProgress skipped (_playlist=null): {Path.GetFileName(path)}={percent}%");
                return;
            }

            Log.Debug($"VideoProgress -> UpdateThumbnailProgress: {Path.GetFileName(path)}={percent}%");
            _playlist.UpdateThumbnailProgress(path, percent);
        });
    }
}
