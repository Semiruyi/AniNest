using AniNest.Application.Playlist;
using AniNest.Application.Thumbnail;
using AniNest.Contracts.Thumbnails;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class ThumbnailFolderProjection
{
    private readonly ThumbnailService _thumbnails;
    private readonly PlaylistCatalogService _playlists;

    public ThumbnailFolderProjection(
        ThumbnailService thumbnails,
        PlaylistCatalogService playlists)
    {
        _thumbnails = thumbnails;
        _playlists = playlists;
    }

    public IReadOnlyList<ThumbnailStatusDto> GetByFolder(string folderId)
    {
        var existing = _thumbnails.GetByFolderId(folderId);
        var playlist = _playlists.GetPlaylist(folderId);
        var existingByTargetId = existing.ToDictionary(
            item => item.TargetId,
            StringComparer.OrdinalIgnoreCase);

        return playlist.Items
            .Select(item => existingByTargetId.TryGetValue(item.ItemId, out var status)
                ? status
                : CreatePendingStatus(item.ItemId))
            .ToArray();
    }

    public ThumbnailFolderSummaryDto GetFolderSummary(string folderId)
    {
        var playlist = _playlists.GetPlaylist(folderId);
        var existing = _thumbnails.GetByFolderId(folderId);
        if (existing.Count == 0)
        {
            return new ThumbnailFolderSummaryDto(
                folderId,
                playlist.Items.Count,
                playlist.Items.Count,
                0,
                0,
                0,
                0,
                null);
        }

        var tracked = existing
            .Select(item => item.TargetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ready = existing.Count(item => item.State == ThumbnailState.Ready);
        var generating = existing.Count(item => item.State == ThumbnailState.Generating);
        var failed = existing.Count(item => item.State == ThumbnailState.Failed);
        var pending = playlist.Items.Count(item => !tracked.Contains(item.ItemId))
            + existing.Count(item => item.State == ThumbnailState.Pending);
        var updatedAtUtc = existing
            .Where(item => item.UpdatedAtUtc.HasValue)
            .Select(item => item.UpdatedAtUtc)
            .Max();
        var total = playlist.Items.Count;
        var completionPercent = total == 0 ? 0 : (double)ready / total * 100;

        return new ThumbnailFolderSummaryDto(
            folderId,
            total,
            pending,
            generating,
            ready,
            failed,
            completionPercent,
            updatedAtUtc);
    }

    public ThumbnailStatusDto? GetByVideo(string videoId)
    {
        var existing = _thumbnails.GetByTargetId(videoId);
        if (existing is not null)
            return existing;

        var (_, item) = _playlists.ResolveItem(videoId);
        return CreatePendingStatus(item.ItemId);
    }

    private static ThumbnailStatusDto CreatePendingStatus(string targetId)
        => new(
            targetId,
            ThumbnailState.Pending,
            0,
            null,
            null);
}
