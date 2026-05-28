using AniNest.Application.Metadata;
using AniNest.Application.Resources;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Events;

internal static class MetadataEventPayloadMapper
{
    public static MetadataFolderUpdatedEventPayload BuildFolderUpdated(
        string folderId,
        MetadataDto? metadata,
        MetadataFolderStateSummary summary,
        IResourceUrlService resourceUrlService)
        => new(
            folderId,
            metadata?.State.ToString(),
            metadata?.FailureKind.ToString(),
            summary.HasMetadata,
            metadata?.Title ?? summary.Title,
            metadata?.OriginalTitle,
            summary.Title,
            string.IsNullOrWhiteSpace(summary.PosterPath)
                ? null
                : resourceUrlService.GetUrl(new ResourceKey(ResourceKind.LibraryPoster, folderId)),
            !summary.HasMetadata
                ? null
                : resourceUrlService.GetUrl(new ResourceKey(ResourceKind.LibraryCover, folderId)),
            DateTimeOffset.UtcNow);
}
