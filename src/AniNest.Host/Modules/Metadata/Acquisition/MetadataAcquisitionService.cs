using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataAcquisitionService : IMetadataAcquisitionService
{
    private readonly IAnimeMetadataProvider _provider;

    public MetadataAcquisitionService(IAnimeMetadataProvider provider)
    {
        _provider = provider;
    }

    public async Task<MetadataAcquisitionResult> AcquireAsync(
        MetadataPreparedContext context,
        CancellationToken cancellationToken)
    {
        var search = await _provider.SearchBestMatchAsync(context.KeywordPlan, cancellationToken);
        if (!search.IsMatch || string.IsNullOrWhiteSpace(search.SourceId))
            return new MetadataAcquisitionResult(false, [], search.FailureReason ?? "No provider candidate.");

        var detail = await _provider.GetSubjectAsync(search.SourceId, cancellationToken);
        return new MetadataAcquisitionResult(
            true,
            [new MetadataAcquisitionCandidate(search.SourceId, search.MatchedTitle, detail)],
            null);
    }
}
