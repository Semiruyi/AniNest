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
    private readonly ISettingsService _settings;

    public BangumiMetadataProvider(ISettingsService settings, HttpClient? httpClient = null)
    {
        _settings = settings;
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<MetadataFetchResult> FetchAsync(MetadataFolderRef folder, CancellationToken ct = default)
    {
        string keyword = BuildKeyword(folder);
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 3)
            return MetadataFetchResult.NoMatch();

        try
        {
            ApplyAuthentication();

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
            var candidate = PickBestCandidate(searchResult?.Data, keyword);
            if (candidate == null)
                return MetadataFetchResult.NoMatch();

            using var detailResponse = await _httpClient.GetAsync($"subjects/{candidate.Id}", ct);
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

    private static BangumiSubjectItem? PickBestCandidate(
        IReadOnlyList<BangumiSubjectItem>? candidates,
        string keyword)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        string normalizedKeyword = Normalize(keyword);
        BangumiSubjectItem? best = null;
        double bestScore = 0;

        foreach (var candidate in candidates)
        {
            string title = FirstNonEmpty(candidate.NameCn, candidate.Name);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            double score = CalculateSimilarity(normalizedKeyword, Normalize(title));
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return bestScore >= 0.6 ? best : null;
    }

    private static string BuildKeyword(MetadataFolderRef folder)
    {
        string keyword = folder.FolderName
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Trim();

        return keyword;
    }

    private static string Normalize(string value)
    {
        var buffer = new char[value.Length];
        int index = 0;

        foreach (char ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                buffer[index++] = ch;
        }

        return new string(buffer, 0, index);
    }

    private static double CalculateSimilarity(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return 0;

        if (string.Equals(left, right, StringComparison.Ordinal))
            return 1;

        int distance = LevenshteinDistance(left, right);
        int maxLength = Math.Max(left.Length, right.Length);
        return maxLength == 0 ? 1 : 1d - (double)distance / maxLength;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var costs = new int[right.Length + 1];
        for (int j = 0; j <= right.Length; j++)
            costs[j] = j;

        for (int i = 1; i <= left.Length; i++)
        {
            int previous = costs[0];
            costs[0] = i;

            for (int j = 1; j <= right.Length; j++)
            {
                int current = costs[j];
                int substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previous + substitutionCost);
                previous = current;
            }
        }

        return costs[right.Length];
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

    private sealed class BangumiSubjectItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_cn")]
        public string? NameCn { get; set; }
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
