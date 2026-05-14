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
