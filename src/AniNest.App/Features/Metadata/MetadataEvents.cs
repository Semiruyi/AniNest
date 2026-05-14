namespace AniNest.Features.Metadata;

public sealed class MetadataEventHub : IMetadataEvents
{
    public event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
    public event EventHandler<MetadataSummaryChangedEventArgs>? SummaryChanged;

    internal void RaiseFolderMetadataRefreshed(string folderPath, FolderMetadata metadata)
        => FolderMetadataRefreshed?.Invoke(this, new FolderMetadataRefreshedEventArgs(folderPath, metadata));

    internal void RaiseSummaryChanged(MetadataStatusSummary summary)
        => SummaryChanged?.Invoke(this, new MetadataSummaryChangedEventArgs(summary));
}
