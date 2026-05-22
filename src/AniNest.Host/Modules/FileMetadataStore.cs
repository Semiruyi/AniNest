using System.Text.Json;
using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal sealed class FileMetadataStore : IMetadataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _metadataPath;
    private readonly IReadOnlyList<MetadataDto> _defaults;
    private readonly object _sync = new();
    private List<MetadataDto>? _cached;

    public FileMetadataStore(string metadataPath, IReadOnlyList<MetadataDto> defaults)
    {
        _metadataPath = metadataPath;
        _defaults = defaults;
    }

    public IReadOnlyList<MetadataDto> GetAll()
        => Load().ToArray();

    public MetadataDto? GetByFolderId(string folderId)
        => Load().FirstOrDefault(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase));

    public void Save(MetadataDto metadata)
    {
        var items = Load();
        var index = items.FindIndex(item => string.Equals(item.FolderId, metadata.FolderId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            items[index] = metadata;
        else
            items.Add(metadata);

        Persist(items);
    }

    private List<MetadataDto> Load()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_metadataPath))
            {
                _cached = _defaults.ToList();
                return _cached;
            }

            using var stream = File.OpenRead(_metadataPath);
            _cached = JsonSerializer.Deserialize<List<MetadataDto>>(stream, SerializerOptions) ?? _defaults.ToList();
            return _cached;
        }
    }

    private void Persist(List<MetadataDto> items)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_metadataPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var stream = File.Create(_metadataPath);
            JsonSerializer.Serialize(stream, items, SerializerOptions);
            _cached = items.ToList();
        }
    }
}
