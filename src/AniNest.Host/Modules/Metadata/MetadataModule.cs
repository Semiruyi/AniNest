using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataModule : IMetadataModule
{
    private readonly IMetadataRuntimeStateService _state;
    private readonly IMetadataReviewService _reviews;
    private readonly IMetadataOrchestrationService _orchestration;

    public MetadataModule(
        IMetadataRuntimeStateService state,
        IMetadataReviewService reviews,
        IMetadataOrchestrationService orchestration)
    {
        _state = state;
        _reviews = reviews;
        _orchestration = orchestration;
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_state.GetMetadata(folderId));

    public Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
        => _reviews.GetReviewQueueAsync(cancellationToken);

    public Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _reviews.GetReviewByFolderAsync(folderId, cancellationToken);

    public Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
        => _reviews.ConfirmReviewAsync(folderId, sourceId, cancellationToken);

    public Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
        => _reviews.RejectReviewCandidateAsync(folderId, sourceId, cancellationToken);

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _orchestration.RefreshFolderAsync(folderId, cancellationToken);

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _orchestration.RetryFolderAsync(folderId, cancellationToken);

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
        => _orchestration.EnqueueMissingAsync(cancellationToken);

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
        => _orchestration.RetryFailedAsync(includeNoMatch, cancellationToken);

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_state.BuildSummary());

    public Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
        => _orchestration.ProcessQueueAsync(maxItems, cancellationToken);
}
