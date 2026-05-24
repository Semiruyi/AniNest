using AniNest.Contracts.Metadata;

namespace AniNest.Application.Metadata;

public interface IMetadataLifecycleService
{
    Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task SyncLibrarySnapshotAsync(IReadOnlyList<MetadataFolderRef> folders, CancellationToken cancellationToken = default);
    Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task EnqueueMissingAsync(CancellationToken cancellationToken = default);
    Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default);
    Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default);
}
