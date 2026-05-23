using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Application.Modules;

public interface ILibraryModule
{
    Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default);
    Task<AddLibraryFolderResult> AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default);
    Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default);
    Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(string folderId, bool isFavorite, CancellationToken cancellationToken = default);
    Task SetWatchStatusAsync(string folderId, WatchStatus status, CancellationToken cancellationToken = default);
    Task MoveFolderToFrontAsync(string folderId, CancellationToken cancellationToken = default);
}
