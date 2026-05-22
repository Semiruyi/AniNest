using System.Text.Json;
using AniNest.Application.Thumbnail;

namespace AniNest.Host.Modules;

internal sealed class FileThumbnailStore : IThumbnailStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _thumbnailPath;
    private readonly IReadOnlyList<ThumbnailRecord> _defaults;
    private readonly object _sync = new();
    private List<ThumbnailRecord>? _cached;

    public FileThumbnailStore(string thumbnailPath, IReadOnlyList<ThumbnailRecord> defaults)
    {
        _thumbnailPath = thumbnailPath;
        _defaults = defaults;
    }

    public IReadOnlyList<ThumbnailRecord> GetAll()
        => Load().ToArray();

    public IReadOnlyList<ThumbnailRecord> GetByFolderId(string folderId)
        => Load()
            .Where(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public ThumbnailRecord? GetByTargetId(string targetId)
        => Load().FirstOrDefault(item => string.Equals(item.TargetId, targetId, StringComparison.OrdinalIgnoreCase));

    public void Save(ThumbnailRecord record)
    {
        var items = Load();
        var index = items.FindIndex(item => string.Equals(item.TargetId, record.TargetId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            items[index] = record;
        else
            items.Add(record);

        Persist(items);
    }

    public void SaveMany(IReadOnlyList<ThumbnailRecord> records)
    {
        var items = Load();
        foreach (var record in records)
        {
            var index = items.FindIndex(item => string.Equals(item.TargetId, record.TargetId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                items[index] = record;
            else
                items.Add(record);
        }

        Persist(items);
    }

    public void DeleteByFolderId(string folderId)
    {
        var items = Load()
            .Where(item => !string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Persist(items);
    }

    private List<ThumbnailRecord> Load()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_thumbnailPath))
            {
                _cached = _defaults.ToList();
                return _cached;
            }

            using var stream = File.OpenRead(_thumbnailPath);
            _cached = JsonSerializer.Deserialize<List<ThumbnailRecord>>(stream, SerializerOptions) ?? _defaults.ToList();
            return _cached;
        }
    }

    private void Persist(List<ThumbnailRecord> items)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_thumbnailPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var stream = File.Create(_thumbnailPath);
            JsonSerializer.Serialize(stream, items, SerializerOptions);
            _cached = items.ToList();
        }
    }
}
