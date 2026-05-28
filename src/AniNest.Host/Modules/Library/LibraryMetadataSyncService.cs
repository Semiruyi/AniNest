using AniNest.Application.Library;
using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class LibraryMetadataSyncService
{
    private readonly LibraryFolderProjection _projection;
    private readonly IMetadataLifecycleService _metadataLifecycle;
    private readonly ILogger<LibraryMetadataSyncService> _logger;

    public LibraryMetadataSyncService(
        LibraryFolderProjection projection,
        IMetadataLifecycleService metadataLifecycle,
        ILogger<LibraryMetadataSyncService> logger)
    {
        _projection = projection;
        _metadataLifecycle = metadataLifecycle;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _projection.LoadFolderSnapshotsAsync(cancellationToken);
        var snapshot = new List<MetadataFolderRef>(folders.Count);
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder.FolderPath))
                continue;

            snapshot.Add(new MetadataFolderRef(
                folder.Folder.FolderId,
                folder.FolderPath,
                folder.FolderName,
                folder.ParentFolderName,
                folder.VideoFiles,
                folder.VideoCount));
        }

        _logger.LogInformation(
            "Library metadata sync requested. FolderCount={FolderCount}",
            snapshot.Count);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
    }
}
