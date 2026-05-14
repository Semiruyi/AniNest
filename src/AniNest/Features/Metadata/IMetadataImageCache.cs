namespace AniNest.Features.Metadata;

public interface IMetadataImageCache
{
    Task<string?> CachePosterAsync(
        string folderPath,
        string? posterUrl,
        CancellationToken ct = default);

    void Delete(string folderPath);
}
