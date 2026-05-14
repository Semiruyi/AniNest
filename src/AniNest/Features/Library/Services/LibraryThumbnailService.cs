using System.IO;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Library.Services;

public sealed class LibraryThumbnailService : ILibraryThumbnailService
{
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IVideoScanner _videoScanner;

    public LibraryThumbnailService(
        IThumbnailGenerator thumbnailGenerator,
        IVideoScanner videoScanner)
    {
        _thumbnailGenerator = thumbnailGenerator;
        _videoScanner = videoScanner;
    }

    public event EventHandler<ThumbnailProgressEventArgs>? ThumbnailProgressChanged
    {
        add => _thumbnailGenerator.ProgressChanged += value;
        remove => _thumbnailGenerator.ProgressChanged -= value;
    }

    public void RegisterFolder(string folderPath, IReadOnlyCollection<string> videoFiles)
    {
        _thumbnailGenerator.RegisterCollection(
            new LibraryCollectionRef(folderPath, LibraryCollectionKind.Folder, Path.GetFileName(folderPath)),
            videoFiles);
    }

    public void DeleteFolder(string folderPath)
        => _thumbnailGenerator.DeleteCollection(folderPath);

    public async Task FocusFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videos = await _videoScanner.GetVideoFilesAsync(path, cancellationToken);
        if (videos.Length == 0)
            return;

        RegisterFolder(path, videos);
        _thumbnailGenerator.FocusCollection(path);
    }

    public async Task PrioritizeFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videos = await _videoScanner.GetVideoFilesAsync(path, cancellationToken);
        if (videos.Length == 0)
            return;

        RegisterFolder(path, videos);
        _thumbnailGenerator.BoostCollection(path);
    }

    public async Task RegenerateFolderThumbnailsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videos = await _videoScanner.GetVideoFilesAsync(path, cancellationToken);
        if (videos.Length == 0)
            return;

        RegisterFolder(path, videos);
        _thumbnailGenerator.ResetCollection(path, boostAfterReset: true);
    }

    public async Task ClearFolderThumbnailCacheAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videos = await _videoScanner.GetVideoFilesAsync(path, cancellationToken);
        if (videos.Length == 0)
            return;

        RegisterFolder(path, videos);
        _thumbnailGenerator.ResetCollection(path, boostAfterReset: false);
    }
}
