using System.IO;
using System.Linq;
using System.Windows;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Player.Services;

public sealed class PlayerThumbnailSyncService : IPlayerThumbnailSyncService
{
    private static readonly Logger Log = AppLog.For<PlayerThumbnailSyncService>();

    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly Action _statusChangedHandler;
    private PlaylistViewModel? _playlist;

    public PlayerThumbnailSyncService(IThumbnailGenerator thumbnailGenerator)
    {
        _thumbnailGenerator = thumbnailGenerator;
        _statusChangedHandler = OnStatusChanged;
    }

    public void Attach(PlaylistViewModel playlist)
    {
        if (ReferenceEquals(_playlist, playlist))
            return;

        if (_playlist != null)
            Detach(_playlist);

        _playlist = playlist;
        _thumbnailGenerator.StatusChanged += _statusChangedHandler;
        SyncPlaylistThumbnailStates();
    }

    public void Detach(PlaylistViewModel playlist)
    {
        if (!ReferenceEquals(_playlist, playlist))
            return;

        _thumbnailGenerator.StatusChanged -= _statusChangedHandler;
        _playlist = null;
    }

    private void OnStatusChanged()
        => Application.Current.Dispatcher.Invoke(SyncPlaylistThumbnailStates);

    private void SyncPlaylistThumbnailStates()
    {
        if (_playlist == null)
            return;

        var snapshot = _thumbnailGenerator.GetStatusSnapshot();
        var activeTasksByPath = snapshot.ActiveTasks
            .Where(task => task.State is ThumbnailState.Generating or ThumbnailState.PausedGenerating)
            .GroupBy(task => task.VideoPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        _playlist.SyncThumbnailVisualStates(activeTasksByPath, _thumbnailGenerator.GetThumbnailState);
    }
}
