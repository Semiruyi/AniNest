using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataModule : IMetadataModule
{
    private readonly IMetadataLifecycleService _lifecycle;

    public MetadataModule(IMetadataLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _lifecycle.GetByFolderAsync(folderId, cancellationToken);

    public Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
        => _lifecycle.GetReviewQueueAsync(cancellationToken);

    public Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _lifecycle.GetReviewByFolderAsync(folderId, cancellationToken);

    public Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
        => _lifecycle.ConfirmReviewAsync(folderId, sourceId, cancellationToken);

    public Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
        => _lifecycle.RejectReviewCandidateAsync(folderId, sourceId, cancellationToken);

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _lifecycle.RefreshFolderAsync(folderId, cancellationToken);

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _lifecycle.RetryFolderAsync(folderId, cancellationToken);

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
        => _lifecycle.EnqueueMissingAsync(cancellationToken);

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
        => _lifecycle.RetryFailedAsync(includeNoMatch, cancellationToken);

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        => _lifecycle.GetSummaryAsync(cancellationToken);

    public Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
        => _lifecycle.ProcessQueueAsync(maxItems, cancellationToken);
}
