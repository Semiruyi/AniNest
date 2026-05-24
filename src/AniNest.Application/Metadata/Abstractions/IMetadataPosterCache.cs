namespace AniNest.Application.Metadata;

public interface IMetadataPosterCache
{
    string Save(string fileName, Stream posterStream);
    void Delete(string relativePath);
}
