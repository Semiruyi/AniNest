using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;
using AniNest.Host.Events;

namespace AniNest.Host.Modules;

internal sealed class PlaybackModule : IPlaylistModule, ISessionModule
{
    private readonly PlaybackSessionEngine _engine;
    private readonly IHostEventStream _events;

    public PlaybackModule(
        IPlaylistCatalogStore playlistStore,
        IPlaybackProgressStore progressStore,
        IHostEventStream events)
    {
        _events = events;
        var playlistCatalog = new PlaylistCatalogService(playlistStore);

        _engine = new PlaybackSessionEngine(
            playlistCatalog,
            new PlayerSettingsDto(1.0, 80, true),
            progressStore);

        if (playlistCatalog.GetAll().Count > 0)
        {
            _engine.ActivateFolder(playlistCatalog.GetAll()[0].FolderId);
        }
    }

    public Task<PlaylistDto> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.GetPlaylist(folderId));

    Task<PlaylistDto?> IPlaylistModule.GetCurrentAsync(CancellationToken cancellationToken)
        => Task.FromResult(_engine.GetCurrentPlaylist());

    public Task<SessionOpenResultDto> ActivateFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var result = _engine.ActivateFolder(folderId);
        PublishSessionChanged("session.changed", result.Session);
        return Task.FromResult(result);
    }

    public Task<SessionOpenResultDto> SelectItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var result = _engine.SelectItem(itemId);
        PublishSessionChanged("session.changed", result.Session);
        return Task.FromResult(result);
    }

    public Task<SessionOpenResultDto> MoveNextAsync(CancellationToken cancellationToken = default)
    {
        var result = _engine.MoveNext();
        PublishSessionChanged("session.changed", result.Session);
        return Task.FromResult(result);
    }

    public Task<SessionOpenResultDto> MovePreviousAsync(CancellationToken cancellationToken = default)
    {
        var result = _engine.MovePrevious();
        PublishSessionChanged("session.changed", result.Session);
        return Task.FromResult(result);
    }

    public Task<SessionStateDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.CurrentSession);

    public Task<SessionOpenResultDto> OpenFolderAsync(SessionOpenFolderRequest request, CancellationToken cancellationToken = default)
        => ActivateFolderAsync(request.FolderId, cancellationToken);

    Task<SessionOpenResultDto> ISessionModule.SelectItemAsync(SessionSelectItemRequest request, CancellationToken cancellationToken)
        => SelectItemAsync(request.ItemId, cancellationToken);

    Task<SessionOpenResultDto> ISessionModule.MoveNextAsync(CancellationToken cancellationToken)
        => MoveNextAsync(cancellationToken);

    Task<SessionOpenResultDto> ISessionModule.MovePreviousAsync(CancellationToken cancellationToken)
        => MovePreviousAsync(cancellationToken);

    public Task ReportProgressAsync(SessionProgressReportRequest request, CancellationToken cancellationToken = default)
    {
        _engine.ReportProgress(request);
        if (_engine.CurrentSession is not null)
        {
            _events.Publish("session.progress_saved", new
            {
                sessionId = _engine.CurrentSession.SessionId,
                _engine.CurrentSession.FolderId,
                request.ItemId,
                request.PositionMs,
                request.DurationMs
            });
            PublishSessionChanged("session.changed", _engine.CurrentSession);
        }
        return Task.CompletedTask;
    }

    public Task CompleteAsync(SessionCompleteRequest request, CancellationToken cancellationToken = default)
    {
        _engine.Complete(request);
        if (_engine.CurrentSession is not null)
        {
            _events.Publish("session.completed", new
            {
                sessionId = _engine.CurrentSession.SessionId,
                _engine.CurrentSession.FolderId,
                request.ItemId
            });
            PublishSessionChanged("session.changed", _engine.CurrentSession);
        }
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        var session = _engine.CurrentSession;
        _engine.Close();
        _events.Publish("session.closed", new
        {
            sessionId = session?.SessionId,
            folderId = session?.FolderId
        });
        return Task.CompletedTask;
    }

    private void PublishSessionChanged(string type, SessionStateDto session)
    {
        _events.Publish(type, new
        {
            session.SessionId,
            session.FolderId,
            session.CurrentItemId,
            session.CurrentIndex,
            session.SavedProgressMs
        });
    }
}
