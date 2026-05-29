using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AniNest.Application.Metadata;
using AniNest.Application.Modules;

namespace AniNest.Host.Modules;

internal sealed class BangumiMetadataProvider : IAnimeMetadataProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ISettingsModule _settings;

    public BangumiMetadataProvider(HttpClient httpClient, ISettingsModule settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ProviderSearchResult>> SearchAsync(
        MetadataKeywordPlan plan,
        string keyword,
        int maxCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        using var request = await CreateRequestAsync(HttpMethod.Post, "v0/search/subjects", cancellationToken);
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
        return payload?.Data?
            .Where(item => item.Id > 0)
            .Take(Math.Max(1, maxCount))
            .Select(item => new ProviderSearchResult(
                item.Id.ToString(),
                string.IsNullOrWhiteSpace(item.NameCn) ? item.Name : item.NameCn,
                item.Name,
                TryParseYear(item.Date),
                "bangumi"))
            .ToArray()
            ?? [];
    }

    public async Task<ProviderSubjectDetail> GetSubjectAsync(string sourceId, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"v0/subjects/{sourceId}", cancellationToken);
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
        var aliases = ExtractAliases(payload);

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
            aliases,
            tags,
            "bangumi");
    }

    public async Task<Stream> DownloadPosterAsync(string imageUrl, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, imageUrl, cancellationToken, allowAbsolute: true);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        bool allowAbsolute = false)
    {
        var requestUri = allowAbsolute ? new Uri(path, UriKind.Absolute) : new Uri(_httpClient.BaseAddress!, path);
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AniNest", "0.1"));

        var settings = await _settings.GetMetadataAsync(cancellationToken);
        var token = settings.BangumiAccessToken;
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

    private static IReadOnlyList<string> ExtractAliases(BangumiSubjectResponse payload)
    {
        var aliases = new List<string>();
        AddAliases(aliases, payload.Alias);

        foreach (var item in payload.Infobox ?? [])
        {
            if (!IsAliasKey(item.Key))
                continue;

            AddAliases(aliases, item.Value);
        }

        return aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsAliasKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return string.Equals(key, "别名", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "別名", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "alias", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddAliases(ICollection<string> aliases, JsonElement? value)
    {
        if (value is null)
            return;

        switch (value.Value.ValueKind)
        {
            case JsonValueKind.String:
                AddAlias(aliases, value.Value.GetString());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddAlias(aliases, item.GetString());
                        continue;
                    }

                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        if (item.TryGetProperty("v", out var v) && v.ValueKind == JsonValueKind.String)
                            AddAlias(aliases, v.GetString());
                        else if (item.TryGetProperty("value", out var valueProperty) && valueProperty.ValueKind == JsonValueKind.String)
                            AddAlias(aliases, valueProperty.GetString());
                    }
                }
                break;
            case JsonValueKind.Object:
                if (value.Value.TryGetProperty("v", out var objectValue) && objectValue.ValueKind == JsonValueKind.String)
                    AddAlias(aliases, objectValue.GetString());
                break;
        }
    }

    private static void AddAlias(ICollection<string> aliases, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            aliases.Add(value);
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
        public string? Date { get; set; }
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
        public JsonElement? Alias { get; set; }
        public List<BangumiInfoboxItem>? Infobox { get; set; }
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

    private sealed class BangumiInfoboxItem
    {
        public string? Key { get; set; }
        public JsonElement? Value { get; set; }
    }
}
