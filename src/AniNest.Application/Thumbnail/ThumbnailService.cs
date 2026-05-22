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
