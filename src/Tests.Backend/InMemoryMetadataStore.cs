using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Backend.Tests;

internal sealed class InMemoryMetadataStore : IMetadataStore
{
    private readonly List<MetadataDto> _items;

    public InMemoryMetadataStore(IReadOnlyList<MetadataDto> items)
    {
        _items = items.ToList();
    }

    public IReadOnlyList<MetadataDto> GetAll()
        => _items.ToArray();

    public MetadataDto? GetByFolderId(string folderId)
        => _items.FirstOrDefault(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase));

    public void Save(MetadataDto metadata)
    {
        var index = _items.FindIndex(item => string.Equals(item.FolderId, metadata.FolderId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _items[index] = metadata;
        else
            _items.Add(metadata);
    }
}
