namespace AniNest.Features.Metadata;

public interface IMetadataRepository
{
    FolderMetadata? Get(string folderPath);
    void Save(FolderMetadata metadata);
    void Delete(string folderPath);
}
