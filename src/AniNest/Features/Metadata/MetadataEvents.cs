namespace AniNest.Features.Metadata;

public interface IMetadataEvents
{
    event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
    event EventHandler<MetadataSummaryChangedEventArgs>? SummaryChanged;
}

public sealed class FolderMetadataRefreshedEventArgs : EventArgs
{
    public FolderMetadataRefreshedEventArgs(string folderPath, FolderMetadata metadata)
    {
        FolderPath = folderPath;
        Metadata = metadata;
    }

    public string FolderPath { get; }
    public FolderMetadata Metadata { get; }
}

public sealed class MetadataSummaryChangedEventArgs : EventArgs
{
    public MetadataSummaryChangedEventArgs(MetadataStatusSummary summary)
    {
        Summary = summary;
    }

    public MetadataStatusSummary Summary { get; }
}

public sealed class MetadataEventHub : IMetadataEvents
{
    public event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
    public event EventHandler<MetadataSummaryChangedEventArgs>? SummaryChanged;

    internal void RaiseFolderMetadataRefreshed(string folderPath, FolderMetadata metadata)
        => FolderMetadataRefreshed?.Invoke(this, new FolderMetadataRefreshedEventArgs(folderPath, metadata));

    internal void RaiseSummaryChanged(MetadataStatusSummary summary)
        => SummaryChanged?.Invoke(this, new MetadataSummaryChangedEventArgs(summary));
}
