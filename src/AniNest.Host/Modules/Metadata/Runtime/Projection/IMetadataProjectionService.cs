using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataProjectionService
{
    MetadataDto? GetMetadata(string folderId, MetadataRecord? record);
    MetadataStatusSummaryDto BuildSummary(IReadOnlyList<MetadataRecord> records);
    MetadataFolderStateSummary BuildFolderStateSummary(MetadataDto? metadata);
}
