using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Application.Metadata;

public sealed class MetadataService
{
    private readonly IMetadataStore _store;

    public MetadataService(IMetadataStore store)
    {
        _store = store;
    }

    public MetadataDto? GetByFolderId(string folderId)
        => _store.GetByFolderId(folderId);

    public IReadOnlyList<MetadataDto> GetAll()
        => _store.GetAll();

    public MetadataStatusSummaryDto GetSummary()
    {
        var all = _store.GetAll();
        return new MetadataStatusSummaryDto(
            all.Count(item => item.State == MetadataState.NeedsMetadata),
            all.Count(item => item.State == MetadataState.Queued),
            all.Count(item => item.State == MetadataState.Scraping),
            all.Count(item => item.State == MetadataState.Ready),
            all.Count(item => item.State == MetadataState.NeedsReview),
            all.Count(item => item.State == MetadataState.Disabled),
            all.Count(item => item.FailureKind == MetadataFailureKind.NetworkError),
            all.Count(item => item.FailureKind == MetadataFailureKind.NoMatch),
            all.Count(item => item.FailureKind == MetadataFailureKind.ProviderError));
    }

    public void RefreshFolder(string folderId)
    {
        var current = _store.GetByFolderId(folderId)
            ?? throw new KeyNotFoundException($"Metadata for folder '{folderId}' was not found.");

        _store.Save(current with
        {
            State = MetadataState.Queued,
            FailureKind = MetadataFailureKind.None
        });
    }

    public void RetryFolder(string folderId)
        => RefreshFolder(folderId);

    public void EnqueueMissing()
    {
        foreach (var item in _store.GetAll().Where(item => item.State == MetadataState.NeedsMetadata))
        {
            _store.Save(item with { State = MetadataState.Queued });
        }
    }

    public void RetryFailed(bool includeNoMatch)
    {
        foreach (var item in _store.GetAll())
        {
            if (item.FailureKind == MetadataFailureKind.None)
                continue;

            if (item.FailureKind == MetadataFailureKind.NoMatch && !includeNoMatch)
                continue;

            _store.Save(item with
            {
                State = MetadataState.Queued,
                FailureKind = MetadataFailureKind.None
            });
        }
    }

    public void Save(MetadataDto metadata)
        => _store.Save(metadata);
}
