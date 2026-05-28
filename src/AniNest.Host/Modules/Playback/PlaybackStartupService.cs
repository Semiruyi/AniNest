using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using Microsoft.Extensions.Hosting;

namespace AniNest.Host.Modules;

internal sealed class PlaybackStartupService : IHostedService
{
    private readonly PlaybackSessionEngine _engine;
    private readonly PlaylistCatalogService _playlistCatalog;

    public PlaybackStartupService(
        PlaybackSessionEngine engine,
        PlaylistCatalogService playlistCatalog)
    {
        _engine = engine;
        _playlistCatalog = playlistCatalog;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_engine.RestoreLastSession())
            return Task.CompletedTask;

        var firstPlaylist = _playlistCatalog.GetAll().FirstOrDefault();
        if (firstPlaylist is not null)
            _engine.ActivateFolder(firstPlaylist.FolderId);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
