namespace AniNest.Features.Metadata;

public interface IMetadataSyncService
{
    Task SyncLibrarySnapshotAsync(
        IReadOnlyList<MetadataFolderRef> folders,
        CancellationToken ct = default);

    Task RegisterFolderAsync(
        MetadataFolderRef folder,
        CancellationToken ct = default);

    Task DeleteFolderAsync(
        string folderPath,
        CancellationToken ct = default);

    Task EnqueueMissingAsync(CancellationToken ct = default);

    Task RetryFailedAsync(
        bool includeNoMatch = false,
        CancellationToken ct = default);

    Task SetAutoMetadataEnabledAsync(
        bool enabled,
        CancellationToken ct = default);
}
