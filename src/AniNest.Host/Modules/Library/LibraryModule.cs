using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Modules;
using AniNest.Application.Resources;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;
using AniNest.Host.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class LibraryModule : ILibraryModule
{
    private readonly LibraryCatalogService _catalog;
    private readonly ILibraryFileScanner _scanner;
    private readonly IMetadataRuntimeBootstrapService _metadataBootstrap;
    private readonly IMetadataLifecycleService _metadataLifecycle;
    private readonly IMetadataRuntimeStateService _metadataState;
    private readonly IHostEventStream _events;
    private readonly ILogger<LibraryModule> _logger;

    public LibraryModule(
        ILibraryCatalogStore store,
        ILibraryFileScanner scanner,
        IResourceUrlService resourceUrlService,
        IMetadataRuntimeBootstrapService metadataBootstrap,
        IMetadataLifecycleService metadataLifecycle,
        IMetadataRuntimeStateService metadataState,
        IHostEventStream events,
        ILogger<LibraryModule> logger)
    {
        _catalog = new LibraryCatalogService(store, scanner, resourceUrlService);
        _scanner = scanner;
        _metadataBootstrap = metadataBootstrap;
        _metadataLifecycle = metadataLifecycle;
        _metadataState = metadataState;
        _events = events;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _catalog.GetFoldersAsync(cancellationToken);
        _logger.LogInformation("Library folders loaded. FolderCount={FolderCount}", folders.Count);
        var snapshot = await BuildSnapshotAsync(folders, cancellationToken);
        _logger.LogInformation("Library metadata snapshot built. FolderCount={FolderCount}", snapshot.Count);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
        return folders.Select(ApplyMetadataSummary).ToArray();
    }

    public async Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _catalog.AddFolderAsync(request, cancellationToken);
        _logger.LogInformation("Library add folder processed. Status={Status}, Path={Path}", result.Status, request.Path);
        if (string.Equals(result.Status, "added", StringComparison.OrdinalIgnoreCase))
        {
            if (result.Folder is not null)
            {
                var snapshot = await BuildSnapshotAsync([result.Folder], cancellationToken);
                await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
            }

            _events.Publish("library.folder_added", new
            {
                folderId = result.Folder?.FolderId,
                path = request.Path
            });
        }

        return result;
    }

    public async Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        var before = await _catalog.GetFoldersAsync(cancellationToken);
        await _catalog.AddFolderBatchAsync(request, cancellationToken);
        var after = await _catalog.GetFoldersAsync(cancellationToken);
        _logger.LogInformation("Library batch add processed. BeforeCount={BeforeCount}, AfterCount={AfterCount}, RootPath={RootPath}", before.Count, after.Count, request.RootPath);
        var snapshot = await BuildSnapshotAsync(after, cancellationToken);
        await _metadataLifecycle.SyncLibrarySnapshotAsync(snapshot, cancellationToken);
        var existingIds = before.Select(folder => folder.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in after.Where(folder => !existingIds.Contains(folder.FolderId)))
        {
            _events.Publish("library.folder_added", new
            {
                folderId = folder.FolderId
            });
        }
    }

    public Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.DeleteFolder(folderId);
        _logger.LogInformation("Library folder deleted. FolderId={FolderId}", folderId);
        _events.Publish("library.folder_removed", new { folderId });
        return Task.CompletedTask;
    }

    public Task SetFavoriteAsync(string folderId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        _catalog.SetFavorite(folderId, isFavorite);
        _events.Publish("library.folder_updated", new { folderId, isFavorite });
        return Task.CompletedTask;
    }

    public Task SetWatchStatusAsync(string folderId, WatchStatus status, CancellationToken cancellationToken = default)
    {
        _catalog.SetWatchStatus(folderId, status);
        _events.Publish("library.folder_updated", new { folderId, watchStatus = status.ToString() });
        return Task.CompletedTask;
    }

    public Task MoveFolderToFrontAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.MoveFolderToFront(folderId);
        _events.Publish("library.folder_reordered", new { folderId, position = 0 });
        return Task.CompletedTask;
    }

    private LibraryFolderDto ApplyMetadataSummary(LibraryFolderDto folder)
    {
        _metadataBootstrap.EnsureInitialized();
        var summary = _metadataState.GetFolderStateSummary(folder.FolderId);
        return _catalog.ApplyMetadataSummary(
            folder,
            summary.Title,
            summary.PosterPath,
            summary.State.ToString(),
            summary.HasMetadata);
    }

    private async Task<IReadOnlyList<MetadataFolderRef>> BuildSnapshotAsync(
        IReadOnlyList<LibraryFolderDto> folders,
        CancellationToken cancellationToken)
    {
        var result = new List<MetadataFolderRef>(folders.Count);
        foreach (var folder in folders)
        {
            var record = _catalog.GetFolderRecord(folder.FolderId);
            if (record is null)
                continue;

            var videoFiles = await _scanner.GetVideoFilesAsync(record.Path, cancellationToken);
            var parentName = Path.GetDirectoryName(record.Path) is { } parentPath
                ? Path.GetFileName(parentPath)
                : null;

            result.Add(new MetadataFolderRef(
                folder.FolderId,
                record.Path,
                record.Name,
                parentName,
                videoFiles,
                record.VideoCount));
        }

        return result;
    }
}
