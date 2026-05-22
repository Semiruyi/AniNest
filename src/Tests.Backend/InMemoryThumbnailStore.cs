using AniNest.Application.Thumbnail;

namespace AniNest.Backend.Tests;

internal sealed class InMemoryThumbnailStore : IThumbnailStore
{
    private readonly List<ThumbnailRecord> _records;

    public InMemoryThumbnailStore(IReadOnlyList<ThumbnailRecord> records)
    {
        _records = records.ToList();
    }

    public IReadOnlyList<ThumbnailRecord> GetAll()
        => _records.ToArray();

    public IReadOnlyList<ThumbnailRecord> GetByFolderId(string folderId)
        => _records.Where(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase)).ToArray();

    public ThumbnailRecord? GetByTargetId(string targetId)
        => _records.FirstOrDefault(item => string.Equals(item.TargetId, targetId, StringComparison.OrdinalIgnoreCase));

    public void Save(ThumbnailRecord record)
    {
        var index = _records.FindIndex(item => string.Equals(item.TargetId, record.TargetId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _records[index] = record;
        else
            _records.Add(record);
    }

    public void SaveMany(IReadOnlyList<ThumbnailRecord> records)
    {
        foreach (var record in records)
            Save(record);
    }

    public void DeleteByFolderId(string folderId)
    {
        _records.RemoveAll(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
    }
}
