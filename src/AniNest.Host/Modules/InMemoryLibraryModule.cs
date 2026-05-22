using AniNest.Application.Modules;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class InMemoryLibraryModule : ILibraryModule
{
    private readonly List<LibraryFolderDto> _folders =
    [
        new(
            "sample-folder",
            "Sample Anime",
            "D:/Media/Sample Anime",
            12,
            null,
            3,
            WatchStatus.Watching,
            true,
            new LibraryMetadataSummaryDto("Sample Anime", null))
    ];

    public Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LibraryFolderDto>>(_folders.ToArray());

    public Task AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetFavoriteAsync(string folderId, bool isFavorite, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetWatchStatusAsync(string folderId, WatchStatus status, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MoveFolderToFrontAsync(string folderId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
