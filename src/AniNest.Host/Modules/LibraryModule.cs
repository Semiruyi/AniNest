using AniNest.Application.Library;
using AniNest.Application.Modules;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;
using AniNest.Host.Events;

namespace AniNest.Host.Modules;

internal sealed class LibraryModule : ILibraryModule
{
    private readonly LibraryCatalogService _catalog;
    private readonly IHostEventStream _events;

    public LibraryModule(ILibraryCatalogStore store, ILibraryFileScanner scanner, IHostEventStream events)
    {
        _catalog = new LibraryCatalogService(store, scanner);
        _events = events;
    }

    public Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
        => _catalog.GetFoldersAsync(cancellationToken);

    public async Task AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        await _catalog.AddFolderAsync(request, cancellationToken);
        var added = (await _catalog.GetFoldersAsync(cancellationToken))
            .FirstOrDefault(folder => string.Equals(folder.Path, request.Path, StringComparison.OrdinalIgnoreCase));
        _events.Publish("library.folder_added", new
        {
            folderId = added?.FolderId,
            path = request.Path
        });
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
                folderId = folder.FolderId,
                folder.Path
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
