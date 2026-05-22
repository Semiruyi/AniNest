using AniNest.Contracts.Thumbnails;

namespace AniNest.Application.Modules;

public interface IThumbnailModule
{
    Task<IReadOnlyList<ThumbnailStatusDto>> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task<ThumbnailStatusDto?> GetByVideoAsync(string videoId, CancellationToken cancellationToken = default);
    Task PrioritizeFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task RegenerateFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task ClearFolderCacheAsync(string folderId, CancellationToken cancellationToken = default);
}
