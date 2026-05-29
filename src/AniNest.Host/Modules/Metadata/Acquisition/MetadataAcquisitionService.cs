using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataAcquisitionService : IMetadataAcquisitionService
{
    private readonly IAnimeMetadataProvider _provider;
    private readonly ILogger<MetadataAcquisitionService> _logger;
    private const int SearchFanoutPerKeyword = 5;
    private const int DetailFanout = 6;
    private const int CandidatePoolLimit = 8;

    public MetadataAcquisitionService(
        IAnimeMetadataProvider provider,
        ILogger<MetadataAcquisitionService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task<MetadataAcquisitionResult> AcquireAsync(
        MetadataPreparedContext context,
        CancellationToken cancellationToken)
    {
        var keywords = MetadataSearchKeywordBuilder.Build(context);
        _logger.LogInformation(
            "Metadata acquisition started. FolderId={FolderId}, Keywords={Keywords}",
            context.Record.FolderId,
            string.Join(" | ", keywords));
        var candidatePool = new Dictionary<string, CandidateSeed>(StringComparer.OrdinalIgnoreCase);
        var prioritySourceIds = new List<string>();
        var searchSucceededCount = 0;
        var failedKeywords = new List<string>();

        for (var keywordIndex = 0; keywordIndex < keywords.Count; keywordIndex++)
        {
            var keyword = keywords[keywordIndex];
            IReadOnlyList<ProviderSearchResult> providerCandidates;
            try
            {
                providerCandidates = await _provider.SearchAsync(
                    context.KeywordPlan,
                    keyword,
                    SearchFanoutPerKeyword,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Metadata acquisition search failed. FolderId={FolderId}, Keyword={Keyword}",
                    context.Record.FolderId,
                    keyword);
                failedKeywords.Add(keyword);
                continue;
            }

            searchSucceededCount++;
            _logger.LogInformation(
                "Metadata acquisition search completed. FolderId={FolderId}, Keyword={Keyword}, CandidateCount={CandidateCount}",
                context.Record.FolderId,
                keyword,
                providerCandidates.Count);

            var rank = 0;
            foreach (var candidate in providerCandidates)
            {
                rank++;
                if (string.IsNullOrWhiteSpace(candidate.SourceId))
                    continue;

                if (rank == 1 &&
                    !prioritySourceIds.Contains(candidate.SourceId, StringComparer.OrdinalIgnoreCase))
                {
                    prioritySourceIds.Add(candidate.SourceId);
                }

                if (candidatePool.TryGetValue(candidate.SourceId, out var existing))
                {
                    existing.RegisterHit(rank, keywordIndex, candidate);
                    continue;
                }

                if (candidatePool.Count >= CandidatePoolLimit)
                    continue;

                candidatePool.Add(candidate.SourceId, new CandidateSeed(candidate, rank, keywordIndex));
            }
        }

        if (candidatePool.Count == 0)
        {
            _logger.LogWarning(
                "Metadata acquisition produced no candidates. FolderId={FolderId}, SearchSucceededCount={SearchSucceededCount}, FailedKeywords={FailedKeywords}",
                context.Record.FolderId,
                searchSucceededCount,
                string.Join(" | ", failedKeywords));
            if (searchSucceededCount == 0 && failedKeywords.Count > 0)
                return new MetadataAcquisitionResult(false, [], $"search_failed:{string.Join(" | ", failedKeywords)}");

            return new MetadataAcquisitionResult(true, [], "no_match");
        }

        var rankedCandidates = BuildHydrationCandidates(candidatePool, prioritySourceIds)
            .Take(DetailFanout)
            .ToArray();

        var hydrated = new List<MetadataAcquisitionCandidate>(DetailFanout);
        foreach (var search in rankedCandidates)
        {
            ProviderSubjectDetail? detail = null;
            try
            {
                detail = await _provider.GetSubjectAsync(search.SourceId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Metadata acquisition detail fetch failed. FolderId={FolderId}, SourceId={SourceId}",
                    context.Record.FolderId,
                    search.SourceId);
            }

            var seed = candidatePool[search.SourceId];
            hydrated.Add(new MetadataAcquisitionCandidate(
                search.SourceId,
                search.MatchedTitle,
                search.OriginalTitle,
                search.Year,
                seed.HitCount,
                seed.BestRank,
                detail));
        }

        if (hydrated.All(candidate => candidate.Detail is null))
            return new MetadataAcquisitionResult(false, hydrated, "detail_failed");

        _logger.LogInformation(
            "Metadata acquisition completed. FolderId={FolderId}, SearchKeywords={KeywordCount}, CandidateCount={CandidateCount}, HydratedCount={HydratedCount}",
            context.Record.FolderId,
            keywords.Count,
            candidatePool.Count,
            hydrated.Count(candidate => candidate.Detail is not null));

        return new MetadataAcquisitionResult(
            true,
            hydrated,
            null);
    }

    private static IReadOnlyList<ProviderSearchResult> BuildHydrationCandidates(
        IReadOnlyDictionary<string, CandidateSeed> candidatePool,
        IReadOnlyList<string> prioritySourceIds)
    {
        var selected = new List<ProviderSearchResult>(candidatePool.Count);
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceId in prioritySourceIds)
        {
            if (!candidatePool.TryGetValue(sourceId, out var prioritizedSeed))
                continue;

            if (!selectedIds.Add(sourceId))
                continue;

            selected.Add(prioritizedSeed.Result);
        }

        foreach (var seed in candidatePool.Values
                     .OrderByDescending(seed => seed.HitCount)
                     .ThenBy(seed => seed.BestRank)
                     .ThenBy(seed => seed.FirstKeywordIndex))
        {
            if (!selectedIds.Add(seed.Result.SourceId))
                continue;

            selected.Add(seed.Result);
        }

        return selected;
    }

    private sealed class CandidateSeed
    {
        public CandidateSeed(ProviderSearchResult result, int rank, int keywordIndex)
        {
            Result = result;
            BestRank = rank;
            FirstKeywordIndex = keywordIndex;
            HitCount = 1;
        }

        public ProviderSearchResult Result { get; private set; }

        public int BestRank { get; private set; }

        public int FirstKeywordIndex { get; }

        public int HitCount { get; private set; }

        public void RegisterHit(int rank, int keywordIndex, ProviderSearchResult candidate)
        {
            HitCount++;
            if (rank < BestRank)
            {
                BestRank = rank;
                Result = candidate;
            }
        }
    }
}
