using AniNest.Application.Library;
using AniNest.Application.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class LibraryMetadataSyncService
{
    private readonly LibraryCatalogService _catalog;
    private readonly ILibraryFileScanner _scanner;
    private readonly IMetadataLifecycleService _metadataLifecycle;
    private readonly ILogger<LibraryMetadataSyncService> _logger;

    public LibraryMetadataSyncService(
        LibraryCatalogService catalog,
        ILibraryFileScanner scanner,
        IMetadataLifecycleService metadataLifecycle,
        ILogger<LibraryMetadataSyncService> logger)
    {
        _catalog = catalog;
        _scanner = scanner;
        _metadataLifecycle = metadataLifecycle;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _catalog.GetFoldersAsync(cancellationToken);
        var snapshot = new List<MetadataFolderRef>(folders.Count);
        foreach (var folder in folders)
        {
            var record = _catalog.GetFolderRecord(folder.FolderId);
            if (record is null)
                continue;

            var videoFiles = await _scanner.GetVideoFilesAsync(record.Path, cancellationToken);
            var parentName = Path.GetDirectoryName(record.Path) is { } parentPath
                ? Path.GetFileName(parentPath)
                : null;

            snapshot.Add(new MetadataFolderRef(
                folder.FolderId,
                record.Path,
                record.Name,
                parentName,
                videoFiles,
                record.VideoCount));
        }

        _logger.LogInformation(
            "Library metadata sync requested. FolderCount={FolderCount}",
            snapshot.Count);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
    }
}
