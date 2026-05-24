namespace AniNest.Application.Metadata;

public interface IAnimeMetadataProvider
{
    Task<IReadOnlyList<ProviderSearchResult>> SearchAsync(
        MetadataKeywordPlan plan,
        string keyword,
        int maxCount,
        CancellationToken cancellationToken);

    Task<ProviderSubjectDetail> GetSubjectAsync(
        string sourceId,
        CancellationToken cancellationToken);

    Task<Stream> DownloadPosterAsync(
        string imageUrl,
        CancellationToken cancellationToken);
}
