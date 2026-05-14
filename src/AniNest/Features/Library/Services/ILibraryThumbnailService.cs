using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Library.Services;

public interface ILibraryThumbnailService
{
    event EventHandler<ThumbnailProgressEventArgs>? ThumbnailProgressChanged;

    void RegisterFolder(string folderPath, IReadOnlyCollection<string> videoFiles);
    void DeleteFolder(string folderPath);
    Task FocusFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default);
    Task PrioritizeFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default);
    Task RegenerateFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default);
    Task ClearFolderThumbnailCacheAsync(string path, CancellationToken cancellationToken = default);
}
