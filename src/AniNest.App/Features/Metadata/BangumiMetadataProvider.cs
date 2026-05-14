using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Features.Metadata;

public sealed class BangumiMetadataProvider : IMetadataProvider
{
    private static readonly Uri SearchUri = new("https://api.bgm.tv/v0/search/subjects?limit=5");
    private static readonly Uri BaseUri = new("https://api.bgm.tv/v0/");
    private readonly HttpClient _httpClient;
    private readonly MetadataMatcher _matcher;
    private readonly ISettingsService _settings;

    public BangumiMetadataProvider(
        ISettingsService settings,
        MetadataMatcher matcher,
        HttpClient? httpClient = null)
    {
        _settings = settings;
        _matcher = matcher;
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<MetadataFetchResult> FetchAsync(MetadataFolderRef folder, CancellationToken ct = default)
    {
        var plans = _matcher.BuildKeywordPlans(folder)
            .Where(plan => !string.IsNullOrWhiteSpace(plan.PrimaryKeyword))
            .ToArray();
        if (plans.Length == 0)
            return MetadataFetchResult.NoMatch();

        try
        {
            ApplyAuthentication();

            BangumiSubjectItem? bestCandidate = null;
            double bestScore = double.MinValue;

            foreach (var plan in plans)
            {
                if (plan.IsAmbiguousShortKeyword)
                    continue;

                foreach (var keyword in _matcher.BuildSearchKeywords(plan))
                {
                    using var searchResponse = await _httpClient.PostAsJsonAsync(
                        SearchUri,
                        new BangumiSearchRequest(
                            keyword,
                            "match",
                            new BangumiSearchFilter([2])),
                        ct);

                    if ((int)searchResponse.StatusCode >= 500)
                        return MetadataFetchResult.NetworkError();

                    if (!searchResponse.IsSuccessStatusCode)
                        return MetadataFetchResult.ProviderError();

                    var searchResult = await searchResponse.Content.ReadFromJsonAsync<BangumiSearchResponse>(cancellationToken: ct);
                    var match = EvaluateBestCandidate(searchResult?.Data, plan);
                    if (match.Candidate != null && match.Score > bestScore)
                    {
                        bestCandidate = match.Candidate;
                        bestScore = match.Score;
                    }
                }
            }

            if (bestCandidate == null)
                return MetadataFetchResult.NoMatch();

            using var detailResponse = await _httpClient.GetAsync($"subjects/{bestCandidate.Id}", ct);
            if ((int)detailResponse.StatusCode >= 500)
                return MetadataFetchResult.NetworkError();

            if (!detailResponse.IsSuccessStatusCode)
                return MetadataFetchResult.ProviderError();

            var subject = await detailResponse.Content.ReadFromJsonAsync<BangumiSubjectDetail>(cancellationToken: ct);
            if (subject == null)
                return MetadataFetchResult.ProviderError();

            var metadata = new FolderMetadata
            {
                FolderPath = folder.FolderPath,
                Title = FirstNonEmpty(subject.NameCn, subject.Name),
                OriginalTitle = subject.Name,
                Summary = subject.Summary,
                PosterUrl = FirstNonEmpty(subject.Images?.Large, subject.Images?.Common, subject.Images?.Medium),
                Date = subject.Date,
                Rating = subject.Rating?.Score,
                Episodes = subject.TotalEpisodes > 0 ? subject.TotalEpisodes : subject.Eps,
                Platform = subject.Platform,
                Tags = subject.Tags?.Select(tag => tag.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToList() ?? [],
                SourceId = subject.Id.ToString(),
                ScrapedAt = DateTime.UtcNow
            };

            return MetadataFetchResult.Success(metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return MetadataFetchResult.NetworkError();
        }
        catch
        {
            return MetadataFetchResult.ProviderError();
        }
    }

    private void ApplyAuthentication()
    {
        string? token = _settings.Load().BangumiAccessToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    internal static BangumiSubjectItem? PickBestCandidate(
        IReadOnlyList<BangumiSubjectItem>? candidates,
        MetadataKeywordPlan plan)
        => EvaluateBestCandidate(candidates, plan).Candidate;

    private static (BangumiSubjectItem? Candidate, double Score) EvaluateBestCandidate(
        IReadOnlyList<BangumiSubjectItem>? candidates,
        MetadataKeywordPlan plan)
    {
        if (candidates == null || candidates.Count == 0 || plan.IsAmbiguousShortKeyword)
            return (null, double.MinValue);

        string normalizedPrimary = MetadataMatcher.NormalizeForComparison(plan.PrimaryKeyword);
        string normalizedBase = MetadataMatcher.NormalizeForComparison(plan.BaseTitle);
        bool romanizedQuery = MetadataMatcher.IsRomanizedKeyword(plan.BaseTitle);
        var seriesCandidates = GetSeriesCandidates(candidates, normalizedBase, plan);
        BangumiSubjectItem? best = null;
        double bestScore = double.MinValue;

        for (int index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            string rawNameCn = candidate.NameCn ?? string.Empty;
            string rawName = candidate.Name ?? string.Empty;
            string normalizedNameCn = MetadataMatcher.NormalizeForComparison(rawNameCn);
            string normalizedName = MetadataMatcher.NormalizeForComparison(rawName);
            if (string.IsNullOrWhiteSpace(normalizedNameCn) && string.IsNullOrWhiteSpace(normalizedName))
                continue;

            double textScore = Math.Max(
                Math.Max(
                    MetadataMatcher.CalculateSimilarity(normalizedPrimary, normalizedNameCn),
                    MetadataMatcher.CalculateSimilarity(normalizedPrimary, normalizedName)),
                Math.Max(
                    MetadataMatcher.CalculateSimilarity(normalizedBase, normalizedNameCn),
                    MetadataMatcher.CalculateSimilarity(normalizedBase, normalizedName)));

            double score = textScore + Math.Max(0, 0.35 - (index * 0.08));

            int? candidateSeason = GetCandidateSeasonNumber(candidate);
            bool candidateLooksMovie = CandidateLooksMovie(candidate);
            int? inferredSeriesOrder = GetSeriesOrder(seriesCandidates, candidate);
            int? effectiveSeason = candidateSeason ?? inferredSeriesOrder;

            if (plan.SeasonNumber.HasValue)
            {
                if (candidateSeason == plan.SeasonNumber.Value)
                {
                    score += 0.28;
                }
                else if (inferredSeriesOrder == plan.SeasonNumber.Value)
                {
                    score += 0.34;
                }
                else if (plan.SeasonNumber.Value > 1)
                {
                    if (effectiveSeason.HasValue)
                    {
                        int diff = Math.Abs(effectiveSeason.Value - plan.SeasonNumber.Value);
                        score -= 0.4 + Math.Min(0.2, diff * 0.15);
                    }
                    else
                    {
                        score -= 0.45;
                    }
                }
            }
            else if (candidateSeason.HasValue && candidateSeason.Value > 1)
            {
                score -= 0.18;
            }

            if (plan.IsMovieLike)
            {
                score += candidateLooksMovie ? 0.35 : -0.45;
                if (candidateLooksMovie && CandidateLooksMovieZero(candidate))
                    score += 0.18;
            }
            else if (candidateLooksMovie)
            {
                score -= 0.22;
            }

            if (plan.YearHint.HasValue && candidate.DateYear.HasValue)
            {
                int diff = Math.Abs(plan.YearHint.Value - candidate.DateYear.Value);
                if (diff == 0)
                    score += 0.04;
                else if (diff > 1)
                    score -= Math.Min(0.2, diff * 0.04);
            }

            if (romanizedQuery && textScore < 0.12)
                score += 0.06;

            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        double acceptanceThreshold = romanizedQuery ? 0.3 : 0.45;
        return bestScore >= acceptanceThreshold ? (best, bestScore) : (null, bestScore);
    }

    private static int? GetCandidateSeasonNumber(BangumiSubjectItem candidate)
    {
        int? season = MetadataMatcher.ExtractSeasonHint(candidate.NameCn ?? string.Empty);
        if (season.HasValue)
            return season;

        return MetadataMatcher.ExtractSeasonHint(candidate.Name ?? string.Empty);
    }

    private static bool CandidateLooksMovie(BangumiSubjectItem candidate)
        => MetadataMatcher.ContainsMovieMarker(candidate.NameCn ?? string.Empty)
            || MetadataMatcher.ContainsMovieMarker(candidate.Name ?? string.Empty);

    private static bool CandidateLooksMovieZero(BangumiSubjectItem candidate)
        => (candidate.NameCn?.Contains('0') ?? false)
            || (candidate.Name?.Contains('0') ?? false);

    private static List<BangumiSubjectItem> GetSeriesCandidates(
        IReadOnlyList<BangumiSubjectItem> candidates,
        string normalizedBase,
        MetadataKeywordPlan plan)
    {
        var matchingCandidates = candidates
            .Where(candidate =>
            {
                string nameCn = MetadataMatcher.NormalizeForComparison(candidate.NameCn ?? string.Empty);
                string name = MetadataMatcher.NormalizeForComparison(candidate.Name ?? string.Empty);
                return (!string.IsNullOrWhiteSpace(nameCn) && nameCn.Contains(normalizedBase, StringComparison.Ordinal))
                    || (!string.IsNullOrWhiteSpace(name) && name.Contains(normalizedBase, StringComparison.Ordinal));
            })
            .ToList();

        if (matchingCandidates.Count >= 2)
        {
            return matchingCandidates
                .Where(candidate => !IsNonSeriesEntry(candidate))
                .OrderBy(candidate => candidate.DateYear ?? int.MaxValue)
                .ThenBy(candidate => candidate.Id)
                .ToList();
        }

        if (plan.SeasonNumber.HasValue && plan.SeasonNumber.Value > 1)
        {
            return candidates
                .Where(candidate => !IsNonSeriesEntry(candidate))
                .OrderBy(candidate => candidate.DateYear ?? int.MaxValue)
                .ThenBy(candidate => candidate.Id)
                .ToList();
        }

        return matchingCandidates
            .OrderBy(candidate => candidate.DateYear ?? int.MaxValue)
            .ThenBy(candidate => candidate.Id)
            .ToList();
    }

    private static int? GetSeriesOrder(List<BangumiSubjectItem> seriesCandidates, BangumiSubjectItem candidate)
    {
        int index = seriesCandidates.FindIndex(item => item.Id == candidate.Id);
        return index >= 0 ? index + 1 : null;
    }

    private static bool IsNonSeriesEntry(BangumiSubjectItem candidate)
    {
        string platform = candidate.Platform ?? string.Empty;
        string nameCn = candidate.NameCn ?? string.Empty;
        string name = candidate.Name ?? string.Empty;

        if (platform.Contains("剧场", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("劇場", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return nameCn.Contains("总集", StringComparison.Ordinal)
            || nameCn.Contains("総集", StringComparison.Ordinal)
            || name.Contains("総集", StringComparison.Ordinal)
            || name.Contains("compilation", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("27181/AniNest.LocalPlayer");
        return client;
    }

    private sealed record BangumiSearchRequest(
        [property: JsonPropertyName("keyword")] string Keyword,
        [property: JsonPropertyName("sort")] string Sort,
        [property: JsonPropertyName("filter")] BangumiSearchFilter Filter);

    private sealed record BangumiSearchFilter(
        [property: JsonPropertyName("type")] int[] Type);

    private sealed class BangumiSearchResponse
    {
        [JsonPropertyName("data")]
        public List<BangumiSubjectItem> Data { get; set; } = [];
    }

    internal sealed class BangumiSubjectItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_cn")]
        public string? NameCn { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        public int? DateYear
            => DateTime.TryParse(Date, out var parsed) ? parsed.Year : null;
    }

    private sealed class BangumiSubjectDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_cn")]
        public string? NameCn { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("eps")]
        public int Eps { get; set; }

        [JsonPropertyName("total_episodes")]
        public int TotalEpisodes { get; set; }

        [JsonPropertyName("images")]
        public BangumiSubjectImages? Images { get; set; }

        [JsonPropertyName("rating")]
        public BangumiRating? Rating { get; set; }

        [JsonPropertyName("tags")]
        public List<BangumiTag>? Tags { get; set; }
    }

    private sealed class BangumiSubjectImages
    {
        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("common")]
        public string? Common { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }
    }

    private sealed class BangumiRating
    {
        [JsonPropertyName("score")]
        public double? Score { get; set; }
    }

    private sealed class BangumiTag
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
