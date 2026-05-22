using AniNest.Application.Library;
using AniNest.Application.Modules;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class LibraryModule : ILibraryModule
{
    private readonly LibraryCatalogService _catalog;

    public LibraryModule(ILibraryCatalogStore store, ILibraryFileScanner scanner)
    {
        _catalog = new LibraryCatalogService(store, scanner);
    }

    public Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
        => _catalog.GetFoldersAsync(cancellationToken);

    public Task AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
        => _catalog.AddFolderAsync(request, cancellationToken);

    public Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
        => _catalog.AddFolderBatchAsync(request, cancellationToken);

    public Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.DeleteFolder(folderId);
        return Task.CompletedTask;
    }

    public Task SetFavoriteAsync(string folderId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        _catalog.SetFavorite(folderId, isFavorite);
        return Task.CompletedTask;
    }

    public Task SetWatchStatusAsync(string folderId, WatchStatus status, CancellationToken cancellationToken = default)
    {
        _catalog.SetWatchStatus(folderId, status);
        return Task.CompletedTask;
    }

    public Task MoveFolderToFrontAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _catalog.MoveFolderToFront(folderId);
        return Task.CompletedTask;
    }
}
