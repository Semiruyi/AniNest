using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AniNest.Application.Metadata;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal sealed class BangumiMetadataProvider : IAnimeMetadataProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly Func<MetadataSettingsDto> _settingsAccessor;

    public BangumiMetadataProvider(HttpClient httpClient, Func<MetadataSettingsDto> settingsAccessor)
    {
        _httpClient = httpClient;
        _settingsAccessor = settingsAccessor;
    }

    public async Task<ProviderSearchResult> SearchBestMatchAsync(
        MetadataKeywordPlan plan,
        CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(plan.PrimaryKeyword)
            ? plan.BaseTitle
            : plan.PrimaryKeyword;
        if (string.IsNullOrWhiteSpace(keyword))
            return new ProviderSearchResult(false, null, null, "empty_keyword");

        using var request = CreateRequest(HttpMethod.Post, "v0/search/subjects");
        var body = JsonSerializer.Serialize(new
        {
            keyword,
            filter = new
            {
                type = new[] { 2 }
            }
        }, SerializerOptions);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<BangumiSearchResponse>(stream, SerializerOptions, cancellationToken);
        var first = payload?.Data?.FirstOrDefault();
        if (first is null)
            return new ProviderSearchResult(false, null, null, "no_match");

        var title = string.IsNullOrWhiteSpace(first.NameCn) ? first.Name : first.NameCn;
        return new ProviderSearchResult(true, first.Id.ToString(), title, null);
    }

    public async Task<ProviderSubjectDetail> GetSubjectAsync(string sourceId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"v0/subjects/{sourceId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<BangumiSubjectResponse>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Bangumi subject response was empty.");

        var tags = payload.Tags?
            .Select(tag => tag.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray()
            ?? Array.Empty<string>();

        return new ProviderSubjectDetail(
            payload.Id.ToString(),
            payload.NameCn ?? payload.Name,
            payload.Name,
            payload.Summary,
            payload.Images?.Large ?? payload.Images?.Common ?? payload.Images?.Medium,
            payload.Date,
            TryParseYear(payload.Date),
            payload.Rating?.Score,
            payload.Eps,
            tags,
            "bangumi");
    }

    public async Task<Stream> DownloadPosterAsync(string imageUrl, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, imageUrl, allowAbsolute: true);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool allowAbsolute = false)
    {
        var requestUri = allowAbsolute ? new Uri(path, UriKind.Absolute) : new Uri(_httpClient.BaseAddress!, path);
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AniNest", "0.1"));

        var token = _settingsAccessor().BangumiAccessToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static int? TryParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4)
            return null;

        return int.TryParse(date[..4], out var year) ? year : null;
    }

    private sealed class BangumiSearchResponse
    {
        public List<BangumiSearchSubject>? Data { get; set; }
    }

    private sealed class BangumiSearchSubject
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? NameCn { get; set; }
    }

    private sealed class BangumiSubjectResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? NameCn { get; set; }
        public string? Summary { get; set; }
        public string? Date { get; set; }
        public int? Eps { get; set; }
        public BangumiImages? Images { get; set; }
        public BangumiRating? Rating { get; set; }
        public List<BangumiTag>? Tags { get; set; }
    }

    private sealed class BangumiImages
    {
        public string? Large { get; set; }
        public string? Common { get; set; }
        public string? Medium { get; set; }
    }

    private sealed class BangumiRating
    {
        public double? Score { get; set; }
    }

    private sealed class BangumiTag
    {
        public string? Name { get; set; }
    }
}
