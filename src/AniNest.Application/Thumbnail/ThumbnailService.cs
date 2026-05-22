using AniNest.Contracts.Thumbnails;

namespace AniNest.Application.Thumbnail;

public sealed class ThumbnailService
{
    private readonly IThumbnailStore _store;

    public ThumbnailService(IThumbnailStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ThumbnailStatusDto> GetByFolderId(string folderId)
        => _store.GetByFolderId(folderId).Select(Map).ToArray();

    public ThumbnailStatusDto? GetByTargetId(string targetId)
        => _store.GetByTargetId(targetId) is { } record ? Map(record) : null;

    public ThumbnailFolderSummaryDto GetFolderSummary(string folderId, int totalItems)
    {
        var records = _store.GetByFolderId(folderId);
        var pending = records.Count(record => record.State == Core.Enums.ThumbnailState.Pending);
        var generating = records.Count(record => record.State == Core.Enums.ThumbnailState.Generating);
        var ready = records.Count(record => record.State == Core.Enums.ThumbnailState.Ready);
        var failed = records.Count(record => record.State == Core.Enums.ThumbnailState.Failed);
        var updatedAt = records
            .Where(record => record.UpdatedAtUtc.HasValue)
            .Select(record => record.UpdatedAtUtc)
            .Max();

        var effectiveTotal = totalItems > 0 ? totalItems : records.Count;
        var completionPercent = effectiveTotal == 0 ? 0 : (double)ready / effectiveTotal * 100;

        return new ThumbnailFolderSummaryDto(
            folderId,
            effectiveTotal,
            pending,
            generating,
            ready,
            failed,
            completionPercent,
            updatedAt);
    }

    public void SaveMany(IReadOnlyList<ThumbnailRecord> records)
        => _store.SaveMany(records);

    public void ClearFolder(string folderId)
        => _store.DeleteByFolderId(folderId);

    private static ThumbnailStatusDto Map(ThumbnailRecord record)
        => new(
            record.TargetId,
            record.State,
            record.ProgressPercent,
            record.ImagePath,
            record.UpdatedAtUtc);
}
