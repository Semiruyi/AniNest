using AniNest.Application.Modules;
using AniNest.Application.Playlist;
using AniNest.Application.Thumbnail;
using AniNest.Contracts.Thumbnails;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class ThumbnailModule : IThumbnailModule
{
    private readonly ThumbnailService _thumbnails;
    private readonly PlaylistCatalogService _playlists;

    public ThumbnailModule(IThumbnailStore store, IPlaylistCatalogStore playlistStore)
    {
        _thumbnails = new ThumbnailService(store);
        _playlists = new PlaylistCatalogService(playlistStore);
    }

    public Task<IReadOnlyList<ThumbnailStatusDto>> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var existing = _thumbnails.GetByFolderId(folderId);
        if (existing.Count > 0)
            return Task.FromResult(existing);

        var playlist = _playlists.GetPlaylist(folderId);
        var derived = playlist.Items
            .Select(item => new ThumbnailStatusDto(
                item.ItemId,
                ThumbnailState.Pending,
                0,
                null,
                null))
            .ToArray();
        return Task.FromResult<IReadOnlyList<ThumbnailStatusDto>>(derived);
    }

    public Task<ThumbnailStatusDto?> GetByVideoAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var existing = _thumbnails.GetByTargetId(videoId);
        if (existing is not null)
            return Task.FromResult<ThumbnailStatusDto?>(existing);

        var (_, item) = _playlists.ResolveItem(videoId);
        return Task.FromResult<ThumbnailStatusDto?>(new ThumbnailStatusDto(
            item.ItemId,
            ThumbnailState.Pending,
            0,
            null,
            null));
    }

    public Task PrioritizeFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var playlist = _playlists.GetPlaylist(folderId);
        var now = DateTimeOffset.UtcNow;
        var records = playlist.Items
            .Select(item => new ThumbnailRecord(
                folderId,
                item.ItemId,
                ThumbnailState.Generating,
                0,
                null,
                now))
            .ToArray();
        _thumbnails.SaveMany(records);
        return Task.CompletedTask;
    }

    public Task RegenerateFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var playlist = _playlists.GetPlaylist(folderId);
        var now = DateTimeOffset.UtcNow;
        var records = playlist.Items
            .Select(item => new ThumbnailRecord(
                folderId,
                item.ItemId,
                ThumbnailState.Pending,
                0,
                null,
                now))
            .ToArray();
        _thumbnails.SaveMany(records);
        return Task.CompletedTask;
    }

    public Task ClearFolderCacheAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _thumbnails.ClearFolder(folderId);
        return Task.CompletedTask;
    }
}
