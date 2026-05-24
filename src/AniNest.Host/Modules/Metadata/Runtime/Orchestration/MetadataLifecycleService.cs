using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataLifecycleService : IMetadataLifecycleService
{
    private readonly IMetadataRuntimeBootstrapService _bootstrap;
    private readonly IMetadataRuntimeStateService _state;
    private readonly IMetadataReviewService _reviews;
    private readonly IMetadataOrchestrationService _orchestration;

    public MetadataLifecycleService(
        IMetadataRuntimeBootstrapService bootstrap,
        IMetadataRuntimeStateService state,
        IMetadataReviewService reviews,
        IMetadataOrchestrationService orchestration)
    {
        _bootstrap = bootstrap;
        _state = state;
        _reviews = reviews;
        _orchestration = orchestration;
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        return Task.FromResult(_state.GetMetadata(folderId));
    }

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        return Task.FromResult(_state.BuildSummary());
    }

    public Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
        => _reviews.GetReviewQueueAsync(cancellationToken);

    public Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _reviews.GetReviewByFolderAsync(folderId, cancellationToken);

    public Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
        => _reviews.ConfirmReviewAsync(folderId, sourceId, cancellationToken);

    public Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
        => _reviews.RejectReviewCandidateAsync(folderId, sourceId, cancellationToken);

    public Task SyncLibrarySnapshotAsync(IReadOnlyList<MetadataFolderRef> folders, CancellationToken cancellationToken = default)
        => _orchestration.SyncLibrarySnapshotAsync(folders, cancellationToken);

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _orchestration.RefreshFolderAsync(folderId, cancellationToken);

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => _orchestration.RetryFolderAsync(folderId, cancellationToken);

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
        => _orchestration.EnqueueMissingAsync(cancellationToken);

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
        => _orchestration.RetryFailedAsync(includeNoMatch, cancellationToken);

    public Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
        => _orchestration.ProcessQueueAsync(maxItems, cancellationToken);

    public MetadataFolderStateSummary GetFolderStateSummary(string folderId)
    {
        _bootstrap.EnsureInitialized();
        return _state.GetFolderStateSummary(folderId);
    }
}
