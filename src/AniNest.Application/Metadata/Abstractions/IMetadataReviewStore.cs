namespace AniNest.Application.Metadata;

public interface IMetadataReviewStore
{
    IReadOnlyList<MetadataReviewRecord> GetAll();
    MetadataReviewRecord? GetByFolderId(string folderId);
    void Save(MetadataReviewRecord record);
    void Delete(string folderId);
}
