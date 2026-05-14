namespace AniNest.Features.Metadata;

public interface IMetadataQueryService
{
    FolderMetadata? GetMetadata(string folderPath);
    MetadataState GetState(string folderPath);
    MetadataStatusSummary GetSummary();

    event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
    event EventHandler<MetadataSummaryChangedEventArgs>? SummaryChanged;
}
