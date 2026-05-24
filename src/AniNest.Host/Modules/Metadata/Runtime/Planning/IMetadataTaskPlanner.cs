using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataTaskPlanner
{
    MetadataLibrarySyncPlan BuildLibrarySyncPlan(
        IReadOnlyList<MetadataRecord> records,
        IReadOnlyList<MetadataFolderRef> folders);

    MetadataPlannedRecordUpdate BuildRefreshPlan(MetadataRecord record);

    IReadOnlyList<MetadataPlannedRecordUpdate> BuildEnqueueMissingPlan(IReadOnlyList<MetadataRecord> records);

    IReadOnlyList<MetadataPlannedRecordUpdate> BuildRetryFailedPlan(
        IReadOnlyList<MetadataRecord> records,
        bool includeNoMatch);
}
