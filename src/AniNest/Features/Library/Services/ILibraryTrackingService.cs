using AniNest.Infrastructure.Persistence;

namespace AniNest.Features.Library.Services;

public interface ILibraryTrackingService
{
    LibraryFolderTrackingSnapshot GetFolderTrackingSnapshot(string folderPath, string[] videoFiles);
    Task<LibraryFolderDto?> ClearFolderWatchHistoryAsync(string path, CancellationToken cancellationToken = default);
    Task SetFolderWatchStatusAsync(string path, WatchStatus status, CancellationToken cancellationToken = default);
    Task SetFolderFavoriteAsync(string path, bool isFavorite, CancellationToken cancellationToken = default);
}
