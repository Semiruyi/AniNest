namespace AniNest.Application.Metadata;

public interface IMetadataRecordStore
{
    IReadOnlyList<MetadataRecord> GetAll();
    MetadataRecord? GetByFolderId(string folderId);
    void Save(MetadataRecord record);
    void Delete(string folderId);
}
