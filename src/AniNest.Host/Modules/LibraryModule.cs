using AniNest.Application.Library;
using AniNest.Application.Modules;
using AniNest.Application.Resources;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;
using AniNest.Host.Events;

namespace AniNest.Host.Modules;

internal sealed class LibraryModule : ILibraryModule
{
    private readonly LibraryCatalogService _catalog;
    private readonly IHostEventStream _events;

    public LibraryModule(
        ILibraryCatalogStore store,
        ILibraryFileScanner scanner,
        IResourceUrlService resourceUrlService,
        IHostEventStream events)
    {
        _catalog = new LibraryCatalogService(store, scanner, resourceUrlService);
        _events = events;
    }

    public Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
        => _catalog.GetFoldersAsync(cancellationToken);

    public async Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _catalog.AddFolderAsync(request, cancellationToken);
        if (string.Equals(result.Status, "added", StringComparison.OrdinalIgnoreCase))
        {
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
}
