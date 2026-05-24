using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataRuntimeStateService
{
    IReadOnlyList<MetadataRecord> GetAllRecords();
    MetadataRecord? GetRecord(string folderId);
    MetadataRecord RequireRecord(string folderId);
    void SaveRecord(MetadataRecord record);
    void DeleteRecord(string folderId);
    MetadataDto? GetMetadata(string folderId);
    MetadataStatusSummaryDto BuildSummary();
    MetadataFolderStateSummary GetFolderStateSummary(string folderId);
    void PublishFolderState(string folderId);
    void PublishSummaryChanged();
    Task ExecuteAsync(MetadataRecord record, CancellationToken cancellationToken);
}
