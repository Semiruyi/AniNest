namespace AniNest.Application.Thumbnail;

public interface IThumbnailStore
{
    IReadOnlyList<ThumbnailRecord> GetAll();
    IReadOnlyList<ThumbnailRecord> GetByFolderId(string folderId);
    ThumbnailRecord? GetByTargetId(string targetId);
    void Save(ThumbnailRecord record);
    void SaveMany(IReadOnlyList<ThumbnailRecord> records);
    void DeleteByFolderId(string folderId);
}
