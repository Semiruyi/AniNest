using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataLibrarySyncPlan(
    IReadOnlyList<string> RecordIdsToDelete,
    IReadOnlyList<MetadataRecord> RecordsToUpsert,
    IReadOnlyList<MetadataTaskPlan> TaskPlans);
