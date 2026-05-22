using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataModule : IMetadataModule
{
    private readonly MetadataService _metadata;

    public MetadataModule(IMetadataStore store)
    {
        _metadata = new MetadataService(store);
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_metadata.GetByFolderId(folderId));

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _metadata.RefreshFolder(folderId);
        return Task.CompletedTask;
    }

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _metadata.RetryFolder(folderId);
        return Task.CompletedTask;
    }

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
    {
        _metadata.EnqueueMissing();
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
    {
        _metadata.RetryFailed(includeNoMatch);
        return Task.CompletedTask;
    }

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_metadata.GetSummary());
}
