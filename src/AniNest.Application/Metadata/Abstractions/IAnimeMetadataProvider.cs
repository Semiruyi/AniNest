namespace AniNest.Application.Metadata;

public interface IAnimeMetadataProvider
{
    Task<ProviderSearchResult> SearchBestMatchAsync(
        MetadataKeywordPlan plan,
        CancellationToken cancellationToken);

    Task<ProviderSubjectDetail> GetSubjectAsync(
        string sourceId,
        CancellationToken cancellationToken);

    Task<Stream> DownloadPosterAsync(
        string imageUrl,
        CancellationToken cancellationToken);
}
