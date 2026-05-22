using AniNest.Application.Library;
using AniNest.Application.Modules;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class InMemoryLibraryModule : ILibraryModule
{
    private readonly LibraryCatalogService _catalog;

    public InMemoryLibraryModule()
    {
        _catalog = new LibraryCatalogService(new InMemoryLibraryCatalogStore());
    }

    public Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_catalog.GetFolders());

    public Task AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        _catalog.AddFolder(request);
        return Task.CompletedTask;
    }

    public Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        _catalog.AddFolderBatch(request);
        return Task.CompletedTask;
    }

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
