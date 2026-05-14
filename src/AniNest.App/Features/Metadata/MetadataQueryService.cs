namespace AniNest.Features.Metadata;

public sealed class MetadataQueryService : IMetadataQueryService
{
    private readonly IMetadataRepository _metadataRepository;
    private readonly MetadataIndexStore _indexStore;
    private readonly IMetadataEvents _events;

    public MetadataQueryService(
        IMetadataRepository metadataRepository,
        MetadataIndexStore indexStore,
        IMetadataEvents events)
    {
        _metadataRepository = metadataRepository;
        _indexStore = indexStore;
        _events = events;
    }

    public event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed
    {
        add => _events.FolderMetadataRefreshed += value;
        remove => _events.FolderMetadataRefreshed -= value;
    }

    public event EventHandler<MetadataSummaryChangedEventArgs>? SummaryChanged
    {
        add => _events.SummaryChanged += value;
        remove => _events.SummaryChanged -= value;
    }

    public FolderMetadata? GetMetadata(string folderPath)
        => _metadataRepository.Get(folderPath);

    public MetadataState GetState(string folderPath)
    {
        var records = _indexStore.Load();
        return records.TryGetValue(folderPath, out var record)
            ? record.State
            : MetadataState.NeedsMetadata;
    }

    public MetadataStatusSummary GetSummary()
    {
        var records = _indexStore.Load().Values;

        return new MetadataStatusSummary(
            records.Count(record => record.State == MetadataState.NeedsMetadata),
            records.Count(record => record.State == MetadataState.Queued),
            records.Count(record => record.State == MetadataState.Scraping),
            records.Count(record => record.State == MetadataState.Ready),
            records.Count(record => record.State == MetadataState.NeedsReview),
            records.Count(record => record.State == MetadataState.Disabled),
            records.Count(record => record.FailureKind == MetadataFailureKind.NetworkError),
            records.Count(record => record.FailureKind == MetadataFailureKind.NoMatch),
            records.Count(record => record.FailureKind == MetadataFailureKind.ProviderError));
    }
}
