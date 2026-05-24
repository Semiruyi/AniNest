namespace AniNest.Application.Metadata;

public interface IMetadataPayloadRepository
{
    FolderMetadataPayload? Load(string relativePath);
    void Save(string relativePath, FolderMetadataPayload payload);
    void Delete(string relativePath);
}
