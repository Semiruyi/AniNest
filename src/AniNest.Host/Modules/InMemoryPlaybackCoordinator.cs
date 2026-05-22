using AniNest.Application.Modules;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal sealed class InMemoryPlaybackCoordinator : IPlaylistModule, ISessionModule
{
    private readonly PlaybackSessionEngine _engine;

    public InMemoryPlaybackCoordinator()
    {
        var playlistStore = new InMemoryPlaylistCatalogStore();
        var playlistCatalog = new PlaylistCatalogService(playlistStore);
        var progressStore = new InMemoryPlaybackProgressStore();
        progressStore.SaveFolderProgress("sample-folder", "ep-01");
        progressStore.SaveVideoProgress("D:/Media/Sample Anime/01.mp4", 93_000, 1_440_000);

        _engine = new PlaybackSessionEngine(
            playlistCatalog,
            new PlayerSettingsDto(1.0, 80, true),
            progressStore);
        _engine.ActivateFolder("sample-folder");
    }

    public Task<PlaylistDto> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.GetPlaylist(folderId));

    Task<PlaylistDto?> IPlaylistModule.GetCurrentAsync(CancellationToken cancellationToken)
        => Task.FromResult(_engine.GetCurrentPlaylist());

    public Task<SessionOpenResultDto> ActivateFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.ActivateFolder(folderId));

    public Task<SessionOpenResultDto> SelectItemAsync(string itemId, CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.SelectItem(itemId));

    public Task<SessionOpenResultDto> MoveNextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.MoveNext());

    public Task<SessionOpenResultDto> MovePreviousAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_engine.MovePrevious());

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
        return Task.CompletedTask;
    }

    public Task CompleteAsync(SessionCompleteRequest request, CancellationToken cancellationToken = default)
    {
        _engine.Complete(request);
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _engine.Close();
        return Task.CompletedTask;
    }
}
