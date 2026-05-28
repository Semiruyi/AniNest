using AniNest.Application.Library;
using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class LibraryMetadataSyncService
{
    private readonly LibraryFolderProjection _projection;
    private readonly IMetadataOrchestrationService _metadataOrchestration;
    private readonly ILogger<LibraryMetadataSyncService> _logger;

    public LibraryMetadataSyncService(
        LibraryFolderProjection projection,
        IMetadataOrchestrationService metadataOrchestration,
        ILogger<LibraryMetadataSyncService> logger)
    {
        _projection = projection;
        _metadataOrchestration = metadataOrchestration;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _projection.LoadFolderSnapshotsAsync(cancellationToken);
        var snapshot = folders
            .Select(folder => folder.ToMetadataFolderRef())
            .OfType<MetadataFolderRef>()
            .ToArray();

        _logger.LogInformation(
            "Library metadata sync requested. FolderCount={FolderCount}",
            snapshot.Length);
        await _metadataOrchestration.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
    }
}
