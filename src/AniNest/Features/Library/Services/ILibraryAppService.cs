using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Library.Services;

public interface ILibraryAppService
{
    Task<IReadOnlyList<LibraryFolderDto>> LoadLibraryAsync(CancellationToken cancellationToken = default);
    Task<OpenFolderResult> OpenFolderAsync(string path, CancellationToken cancellationToken = default);
    Task<AddFolderResult> AddFolderAsync(string path, CancellationToken cancellationToken = default);
    Task<BatchAddFoldersResult> AddFolderBatchAsync(string rootPath, CancellationToken cancellationToken = default);
    Task MoveFolderToFrontAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteFolderAsync(string path, CancellationToken cancellationToken = default);
    int GetThumbnailExpiryDays();
    ThumbnailExpirySaveResult SaveThumbnailExpiryDays(string input);

    event EventHandler<ThumbnailProgressEventArgs>? ThumbnailProgressChanged;
}
