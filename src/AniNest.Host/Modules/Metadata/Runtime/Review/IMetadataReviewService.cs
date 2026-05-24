using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataReviewService
{
    Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default);
    Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default);
    Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default);
}
