using System.Text.Json;
using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class FileMetadataReviewStore : IMetadataReviewStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _sync = new();
    private List<MetadataReviewRecord>? _cached;

    public FileMetadataReviewStore(string path)
    {
        _path = path;
    }

    public IReadOnlyList<MetadataReviewRecord> GetAll()
        => Load().ToArray();

    public MetadataReviewRecord? GetByFolderId(string folderId)
        => Load().FirstOrDefault(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase));

    public void Save(MetadataReviewRecord record)
    {
        var records = Load();
        var index = records.FindIndex(item => string.Equals(item.FolderId, record.FolderId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            records[index] = record;
        else
            records.Add(record);

        Persist(records);
    }

    public void Delete(string folderId)
    {
        var updated = Load()
            .Where(item => !string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Persist(updated);
    }

    private List<MetadataReviewRecord> Load()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_path))
            {
                _cached = [];
                return _cached;
            }

            using var stream = File.OpenRead(_path);
            _cached = JsonSerializer.Deserialize<List<MetadataReviewRecord>>(stream, SerializerOptions) ?? [];
            return _cached;
        }
    }

    private void Persist(List<MetadataReviewRecord> records)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = _path + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, records, SerializerOptions);
            }

            File.Move(tempPath, _path, true);
            _cached = records.ToList();
        }
    }
}
