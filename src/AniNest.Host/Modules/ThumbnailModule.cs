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
    private readonly IHostEventStream _events;

    public ThumbnailModule(IThumbnailStore store, IPlaylistCatalogStore playlistStore, IHostEventStream events)
    {
        _store = store;
        _thumbnails = new ThumbnailService(store);
        _playlists = new PlaylistCatalogService(playlistStore);
        _events = events;
    }

    public Task<IReadOnlyList<ThumbnailStatusDto>> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var existing = _thumbnails.GetByFolderId(folderId);
        var playlist = _playlists.GetPlaylist(folderId);
        if (existing.Count == 0)
        {
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

        var existingByTargetId = existing.ToDictionary(item => item.TargetId, StringComparer.OrdinalIgnoreCase);
        var merged = playlist.Items
            .Select(item => existingByTargetId.TryGetValue(item.ItemId, out var status)
                ? status
                : new ThumbnailStatusDto(
                    item.ItemId,
                    ThumbnailState.Pending,
                    0,
                    null,
                    null))
            .ToArray();
        return Task.FromResult<IReadOnlyList<ThumbnailStatusDto>>(merged);
    }

    public Task<ThumbnailFolderSummaryDto> GetFolderSummaryAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var playlist = _playlists.GetPlaylist(folderId);
        var existing = _thumbnails.GetByFolderId(folderId);
        if (existing.Count == 0)
        {
            return Task.FromResult(new ThumbnailFolderSummaryDto(
                folderId,
                playlist.Items.Count,
                playlist.Items.Count,
                0,
                0,
                0,
                0,
                null));
        }

        var ready = existing.Count(item => item.State == ThumbnailState.Ready);
        var generating = existing.Count(item => item.State == ThumbnailState.Generating);
        var failed = existing.Count(item => item.State == ThumbnailState.Failed);
        var tracked = existing
            .Select(item => item.TargetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = playlist.Items.Count(item => !tracked.Contains(item.ItemId))
            + existing.Count(item => item.State == ThumbnailState.Pending);
        var updatedAtUtc = existing
            .Where(item => item.UpdatedAtUtc.HasValue)
            .Select(item => item.UpdatedAtUtc)
            .Max();
        var total = playlist.Items.Count;
        var completionPercent = total == 0 ? 0 : (double)ready / total * 100;

        return Task.FromResult(new ThumbnailFolderSummaryDto(
            folderId,
            total,
            pending,
            generating,
            ready,
            failed,
            completionPercent,
            updatedAtUtc));
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
        PublishFolderChanged(folderId, records);
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
        PublishFolderChanged(folderId, records);
        return Task.CompletedTask;
    }

    public Task ClearFolderCacheAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _thumbnails.ClearFolder(folderId);
        PublishFolderChanged(folderId, Array.Empty<ThumbnailRecord>());
        return Task.CompletedTask;
    }

    public Task<ThumbnailProcessingResultDto> ProcessFolderAsync(string folderId, int maxItems, CancellationToken cancellationToken = default)
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
            var unchangedSummary = GetFolderSummaryAsync(folderId, cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(new ThumbnailProcessingResultDto(folderId, 0, unchangedSummary, Array.Empty<string>()));
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
        PublishFolderChanged(folderId, generating);

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
        PublishFolderChanged(folderId, ready);

        var summary = GetFolderSummaryAsync(folderId, cancellationToken).GetAwaiter().GetResult();
        return Task.FromResult(new ThumbnailProcessingResultDto(
            folderId,
            ready.Length,
            summary,
            ready.Select(record => record.TargetId).ToArray()));
    }

    private void PublishFolderChanged(string folderId, IReadOnlyList<ThumbnailRecord> records)
    {
        var summary = GetFolderSummaryAsync(folderId).GetAwaiter().GetResult();
        _events.Publish("thumbnail.folder_updated", new
        {
            folderId,
            generating = records.Count(record => record.State == ThumbnailState.Generating),
            pending = records.Count(record => record.State == ThumbnailState.Pending),
            summary
        });
    }
}
