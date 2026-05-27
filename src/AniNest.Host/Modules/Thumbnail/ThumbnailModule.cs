using AniNest.Application.Modules;
using AniNest.Application.Playlist;
using AniNest.Application.Thumbnail;
using AniNest.Contracts.Thumbnails;
using AniNest.Core.Enums;
using AniNest.Host.Events;

namespace AniNest.Host.Modules;

internal sealed class ThumbnailModule : IThumbnailModule
{
    private readonly IThumbnailStore _store;
    private readonly ThumbnailService _thumbnails;
    private readonly PlaylistCatalogService _playlists;
    private readonly ThumbnailFolderProjection _projection;
    private readonly IHostEventStream _events;

    public ThumbnailModule(IThumbnailStore store, IPlaylistCatalogStore playlistStore, IHostEventStream events)
    {
        _store = store;
        _thumbnails = new ThumbnailService(store);
        _playlists = new PlaylistCatalogService(playlistStore);
        _projection = new ThumbnailFolderProjection(_thumbnails, _playlists);
        _events = events;
    }

    public Task<IReadOnlyList<ThumbnailStatusDto>> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_projection.GetByFolder(folderId));

    public Task<ThumbnailFolderSummaryDto> GetFolderSummaryAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_projection.GetFolderSummary(folderId));

    public Task<ThumbnailStatusDto?> GetByVideoAsync(string videoId, CancellationToken cancellationToken = default)
        => Task.FromResult(_projection.GetByVideo(videoId));

    public async Task PrioritizeFolderAsync(string folderId, CancellationToken cancellationToken = default)
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
        await PublishFolderChangedAsync(folderId, records, cancellationToken);
    }

    public async Task RegenerateFolderAsync(string folderId, CancellationToken cancellationToken = default)
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
        await PublishFolderChangedAsync(folderId, records, cancellationToken);
    }

    public async Task ClearFolderCacheAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _thumbnails.ClearFolder(folderId);
        await PublishFolderChangedAsync(folderId, Array.Empty<ThumbnailRecord>(), cancellationToken);
    }

    public async Task<ThumbnailProcessingResultDto> ProcessFolderAsync(string folderId, int maxItems, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var playlist = _playlists.GetPlaylist(folderId);
        var existing = _store.GetByFolderId(folderId);
        var effectiveExisting = existing.Count > 0
            ? existing
            : playlist.Items.Select(item => new ThumbnailRecord(
                folderId,
                item.ItemId,
                ThumbnailState.Pending,
                0,
                null,
                null))
            .ToArray();

        var candidates = effectiveExisting
            .Where(record => record.State is ThumbnailState.Pending or ThumbnailState.Generating or ThumbnailState.Failed)
            .Take(Math.Max(1, maxItems))
            .ToArray();

        if (candidates.Length == 0)
        {
            var unchangedSummary = await GetFolderSummaryAsync(folderId, cancellationToken);
            return new ThumbnailProcessingResultDto(folderId, 0, unchangedSummary, Array.Empty<string>());
        }

        var now = DateTimeOffset.UtcNow;
        var generating = candidates
            .Select(record => record with
            {
                State = ThumbnailState.Generating,
                ProgressPercent = 25,
                UpdatedAtUtc = now
            })
            .ToArray();
        _thumbnails.SaveMany(generating);
        await PublishFolderChangedAsync(folderId, generating, cancellationToken);

        var ready = generating
            .Select(record => record with
            {
                State = ThumbnailState.Ready,
                ProgressPercent = 100,
                ImagePath = $"/generated/thumbnails/{folderId}/{record.TargetId}.jpg",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            })
            .ToArray();
        _thumbnails.SaveMany(ready);
        await PublishFolderChangedAsync(folderId, ready, cancellationToken);

        var summary = await GetFolderSummaryAsync(folderId, cancellationToken);
        return new ThumbnailProcessingResultDto(
            folderId,
            ready.Length,
            summary,
            ready.Select(record => record.TargetId).ToArray());
    }

    private async Task PublishFolderChangedAsync(
        string folderId,
        IReadOnlyList<ThumbnailRecord> records,
        CancellationToken cancellationToken)
    {
        var summary = await GetFolderSummaryAsync(folderId, cancellationToken);
        _events.Publish("thumbnail.folder_updated", new
        {
            folderId,
            generating = records.Count(record => record.State == ThumbnailState.Generating),
            pending = records.Count(record => record.State == ThumbnailState.Pending),
            summary
        });
    }
}
