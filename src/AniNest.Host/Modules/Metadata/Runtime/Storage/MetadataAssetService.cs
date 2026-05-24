using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataAssetService : IMetadataAssetService
{
    private readonly IMetadataPayloadRepository _payloadRepository;
    private readonly IMetadataPosterCache _posterCache;
    private readonly IAnimeMetadataProvider _provider;
    private readonly ILogger<MetadataAssetService> _logger;

    public MetadataAssetService(
        IMetadataPayloadRepository payloadRepository,
        IMetadataPosterCache posterCache,
        IAnimeMetadataProvider provider,
        ILogger<MetadataAssetService> logger)
    {
        _payloadRepository = payloadRepository;
        _posterCache = posterCache;
        _provider = provider;
        _logger = logger;
    }

    public string? CreateLegacyPayload(MetadataDto metadata)
    {
        var shouldPersist = !string.IsNullOrWhiteSpace(metadata.Title) ||
                            !string.IsNullOrWhiteSpace(metadata.OriginalTitle) ||
                            !string.IsNullOrWhiteSpace(metadata.Summary) ||
                            metadata.Tags.Count > 0 ||
                            !string.IsNullOrWhiteSpace(metadata.Source);
        if (!shouldPersist)
            return null;

        var payloadPath = MetadataStoragePathCodec.GetPayloadPath(metadata.FolderId);
        _payloadRepository.Save(payloadPath, new FolderMetadataPayload(
            metadata.FolderId,
            null,
            metadata.Title,
            metadata.OriginalTitle,
            metadata.Summary,
            null,
            metadata.PosterPath,
            null,
            null,
            null,
            metadata.EpisodeCount,
            metadata.Tags,
            metadata.Source,
            DateTime.UtcNow));
        return payloadPath;
    }

    public async Task<MetadataAssetSnapshot> SaveResolvedPayloadAsync(
        string folderId,
        FolderMetadataPayload payload,
        string? currentMetadataFilePath,
        string? currentPosterFilePath,
        CancellationToken cancellationToken)
    {
        var hydratedPayload = await CachePosterIfAvailableAsync(folderId, payload, currentPosterFilePath, cancellationToken);
        var payloadPath = MetadataStoragePathCodec.GetPayloadPath(folderId);
        _payloadRepository.Save(payloadPath, hydratedPayload);

        if (!string.IsNullOrWhiteSpace(currentMetadataFilePath) &&
            !string.Equals(currentMetadataFilePath, payloadPath, StringComparison.OrdinalIgnoreCase))
        {
            _payloadRepository.Delete(currentMetadataFilePath);
        }

        return new MetadataAssetSnapshot(
            hydratedPayload,
            payloadPath,
            hydratedPayload.LocalPosterPath);
    }

    public MetadataAssetSnapshot SavePlaceholderPayload(
        MetadataRecord record,
        FolderMetadataPayload payload)
    {
        var payloadPath = MetadataStoragePathCodec.GetPayloadPath(record.FolderId);
        _payloadRepository.Save(payloadPath, payload);

        if (!string.IsNullOrWhiteSpace(record.MetadataFilePath) &&
            !string.Equals(record.MetadataFilePath, payloadPath, StringComparison.OrdinalIgnoreCase))
        {
            _payloadRepository.Delete(record.MetadataFilePath);
        }

        if (!string.IsNullOrWhiteSpace(record.PosterFilePath))
            _posterCache.Delete(record.PosterFilePath);

        return new MetadataAssetSnapshot(payload, payloadPath, null);
    }

    public void DeleteAssets(MetadataRecord? record)
        => DeleteAssets(record?.MetadataFilePath, record?.PosterFilePath);

    public void DeleteAssets(string? metadataFilePath, string? posterFilePath)
    {
        if (!string.IsNullOrWhiteSpace(metadataFilePath))
            _payloadRepository.Delete(metadataFilePath);
        if (!string.IsNullOrWhiteSpace(posterFilePath))
            _posterCache.Delete(posterFilePath);
    }

    private async Task<FolderMetadataPayload> CachePosterIfAvailableAsync(
        string folderId,
        FolderMetadataPayload payload,
        string? currentPosterFilePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.PosterUrl))
        {
            if (!string.IsNullOrWhiteSpace(currentPosterFilePath))
                _posterCache.Delete(currentPosterFilePath);
            return payload with { LocalPosterPath = null };
        }

        try
        {
            await using var posterStream = await _provider.DownloadPosterAsync(payload.PosterUrl, cancellationToken);
            var posterFileName = BuildPosterFileName(folderId, payload.SourceId, payload.PosterUrl);
            var savedRelativePath = _posterCache.Save(posterFileName, posterStream);

            if (!string.IsNullOrWhiteSpace(currentPosterFilePath) &&
                !string.Equals(currentPosterFilePath, savedRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                _posterCache.Delete(currentPosterFilePath);
            }

            _logger.LogInformation(
                "Metadata poster cached. FolderId={FolderId}, PosterFilePath={PosterFilePath}",
                folderId,
                savedRelativePath);
            return payload with { LocalPosterPath = savedRelativePath };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Metadata poster cache failed. FolderId={FolderId}, PosterUrl={PosterUrl}",
                folderId,
                payload.PosterUrl);
            return payload with { LocalPosterPath = null };
        }
    }

    private static string BuildPosterFileName(string folderId, string? sourceId, string posterUrl)
    {
        var extension = TryGetPosterExtension(posterUrl);
        var baseName = string.IsNullOrWhiteSpace(sourceId)
            ? folderId
            : $"{folderId}-{sourceId}";
        return $"{SanitizeFileName(baseName)}{extension}";
    }

    private static string TryGetPosterExtension(string posterUrl)
    {
        if (!Uri.TryCreate(posterUrl, UriKind.Absolute, out var uri))
            return ".jpg";

        var extension = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
