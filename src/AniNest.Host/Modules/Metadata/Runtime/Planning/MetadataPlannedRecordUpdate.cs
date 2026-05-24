using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataPlannedRecordUpdate(
    MetadataRecord Record,
    MetadataTaskPlan? TaskPlan);
