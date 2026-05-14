using System.IO;
using System.Net.Http;

namespace AniNest.Features.Metadata;

public sealed class MetadataImageCache : IMetadataImageCache
{
    private readonly HttpClient _httpClient;
    private readonly object _ioLock = new();

    public MetadataImageCache(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string?> CachePosterAsync(
        string folderPath,
        string? posterUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(posterUrl))
            return null;

        using var response = await _httpClient.GetAsync(posterUrl, ct);
        response.EnsureSuccessStatusCode();

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
        string path = MetadataStoragePaths.GetPosterFilePath(folderPath);
        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string tempPath = $"{path}.tmp";

        lock (_ioLock)
        {
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(tempPath, bytes);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        return path;
    }

    public void Delete(string folderPath)
    {
        string path = MetadataStoragePaths.GetPosterFilePath(folderPath);
        lock (_ioLock)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
