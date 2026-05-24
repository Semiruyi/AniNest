using AniNest.Contracts.Metadata;

namespace AniNest.Application.Modules;

public interface IMetadataModule
{
    Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default);
    Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default);
    Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default);
    Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task EnqueueMissingAsync(CancellationToken cancellationToken = default);
    Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default);
    Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default);
}
