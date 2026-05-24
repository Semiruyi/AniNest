using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataAcquisitionService : IMetadataAcquisitionService
{
    private readonly IAnimeMetadataProvider _provider;
    private readonly ILogger<MetadataAcquisitionService> _logger;
    private const int SearchFanoutPerKeyword = 5;
    private const int DetailFanout = 3;
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
        var keywords = BuildSearchKeywords(context);
        var candidatePool = new Dictionary<string, CandidateSeed>(StringComparer.OrdinalIgnoreCase);
        var searchSucceededCount = 0;
        var failedKeywords = new List<string>();

        foreach (var keyword in keywords)
        {
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

                if (candidatePool.TryGetValue(candidate.SourceId, out var existing))
                {
                    existing.RegisterHit(rank, candidate);
                    continue;
                }

                if (candidatePool.Count >= CandidatePoolLimit)
                    continue;

                candidatePool.Add(candidate.SourceId, new CandidateSeed(candidate, rank));
            }
        }

        if (candidatePool.Count == 0)
        {
            if (searchSucceededCount == 0 && failedKeywords.Count > 0)
                return new MetadataAcquisitionResult(false, [], $"search_failed:{string.Join(" | ", failedKeywords)}");

            return new MetadataAcquisitionResult(true, [], "no_match");
        }

        var rankedCandidates = candidatePool.Values
            .OrderByDescending(seed => seed.HitCount)
            .ThenBy(seed => seed.BestRank)
            .Select(seed => seed.Result)
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

    private static IReadOnlyList<string> BuildSearchKeywords(MetadataPreparedContext context)
    {
        var keywords = new List<string>(4);

        AddKeyword(keywords, context.KeywordPlan.PrimaryKeyword);
        AddKeyword(keywords, context.KeywordPlan.SeasonAwareKeyword);
        AddKeyword(keywords, context.KeywordPlan.SimplifiedKeyword);
        AddKeyword(keywords, context.NormalizedTitle);

        foreach (var alias in context.Aliases.Take(3))
            AddKeyword(keywords, alias);

        return keywords;
    }

    private static void AddKeyword(ICollection<string> keywords, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (keywords.Contains(value, StringComparer.OrdinalIgnoreCase))
            return;

        keywords.Add(value);
    }

    private sealed class CandidateSeed
    {
        public CandidateSeed(ProviderSearchResult result, int rank)
        {
            Result = result;
            BestRank = rank;
            HitCount = 1;
        }

        public ProviderSearchResult Result { get; private set; }

        public int BestRank { get; private set; }

        public int HitCount { get; private set; }

        public void RegisterHit(int rank, ProviderSearchResult candidate)
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
