using AniNest.Contracts.Metadata;

namespace AniNest.Application.Metadata;

public interface IMetadataStore
{
    IReadOnlyList<MetadataDto> GetAll();
    MetadataDto? GetByFolderId(string folderId);
    void Save(MetadataDto metadata);
}
