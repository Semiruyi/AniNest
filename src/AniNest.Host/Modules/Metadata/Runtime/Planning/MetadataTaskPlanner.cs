using AniNest.Application.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class MetadataTaskPlanner : IMetadataTaskPlanner
{
    public MetadataLibrarySyncPlan BuildLibrarySyncPlan(
        IReadOnlyList<MetadataRecord> records,
        IReadOnlyList<MetadataFolderRef> folders)
    {
        var recordMap = records.ToDictionary(item => item.FolderId, StringComparer.OrdinalIgnoreCase);
        var knownFolderIds = folders.Select(folder => folder.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recordIdsToDelete = records
            .Where(record => !knownFolderIds.Contains(record.FolderId))
            .Select(record => record.FolderId)
            .ToArray();
        var recordsToUpsert = new List<MetadataRecord>(folders.Count);
        var taskPlans = new List<MetadataTaskPlan>();

        foreach (var folder in folders)
        {
            if (!recordMap.TryGetValue(folder.FolderId, out var existing))
            {
                var created = MetadataStorageDefaults.CreatePlaceholder(folder.FolderId, folder.FolderPath, folder.FolderName);
                recordsToUpsert.Add(created);
                taskPlans.Add(CreatePlan(folder.FolderId, MetadataTaskReason.LibraryReconcile, 20, bypassCooldown: false));
                continue;
            }

            var normalized = existing with
            {
                FolderPath = folder.FolderPath,
                FolderName = folder.FolderName
            };
            recordsToUpsert.Add(normalized);

            if (normalized.State == MetadataState.NeedsMetadata)
                taskPlans.Add(CreatePlan(folder.FolderId, MetadataTaskReason.LibraryReconcile, 20, bypassCooldown: false));
        }

        return new MetadataLibrarySyncPlan(recordIdsToDelete, recordsToUpsert, taskPlans);
    }

    public MetadataPlannedRecordUpdate BuildRefreshPlan(MetadataRecord record)
        => new(
            record with
            {
                State = MetadataState.Queued,
                FailureKind = MetadataFailureKind.None,
                LastAttemptAtUtc = DateTime.UtcNow
            },
            CreatePlan(record.FolderId, MetadataTaskReason.ManualRefresh, 0, bypassCooldown: true));

    public IReadOnlyList<MetadataPlannedRecordUpdate> BuildEnqueueMissingPlan(IReadOnlyList<MetadataRecord> records)
        => records
            .Where(item => item.State == MetadataState.NeedsMetadata)
            .Select(item => new MetadataPlannedRecordUpdate(
                item with { State = MetadataState.Queued },
                CreatePlan(item.FolderId, MetadataTaskReason.MissingMetadata, 30, bypassCooldown: false)))
            .ToArray();

    public IReadOnlyList<MetadataPlannedRecordUpdate> BuildRetryFailedPlan(
        IReadOnlyList<MetadataRecord> records,
        bool includeNoMatch)
        => records
            .Where(item => item.FailureKind != MetadataFailureKind.None)
            .Where(item => includeNoMatch || item.FailureKind != MetadataFailureKind.NoMatch)
            .Select(item => new MetadataPlannedRecordUpdate(
                item with
                {
                    State = MetadataState.Queued,
                    FailureKind = MetadataFailureKind.None,
                    CooldownUntilUtc = null
                },
                CreatePlan(item.FolderId, MetadataTaskReason.RetryFailed, 40, bypassCooldown: true)))
            .ToArray();

    private static MetadataTaskPlan CreatePlan(
        string folderId,
        MetadataTaskReason reason,
        int priority,
        bool bypassCooldown)
        => new(folderId, reason, priority, bypassCooldown);
}
