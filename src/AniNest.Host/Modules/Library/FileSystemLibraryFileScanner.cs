using AniNest.Application.Library;

namespace AniNest.Host.Modules;

internal sealed class FileSystemLibraryFileScanner : ILibraryFileScanner
{
    private static readonly string[] SupportedVideoExtensions =
    [
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".wmv",
        ".m4v",
        ".webm",
        ".flv",
        ".ts"
    ];

    private static readonly string[] SupportedCoverNames =
    [
        "poster.jpg",
        "poster.png",
        "folder.jpg",
        "folder.png",
        "cover.jpg",
        "cover.png"
    ];

    public Task<bool> FolderExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(path));
    }

    public Task<LibraryFolderScanResult> ScanFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videoCount = Directory.EnumerateFiles(path)
            .Count(IsSupportedVideoFile);

        var coverPath = SupportedCoverNames
            .Select(fileName => Path.Combine(path, fileName))
            .FirstOrDefault(File.Exists);

        return Task.FromResult(new LibraryFolderScanResult(videoCount, coverPath));
    }

    public Task<IReadOnlyList<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folders = Directory.EnumerateDirectories(rootPath)
            .Where(ContainsSupportedVideoFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(folders);
    }

    public Task<IReadOnlyList<string>> GetVideoFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetVideoFiles(path));
    }

    public IReadOnlyList<string> GetVideoFiles(string path)
        => Directory.Exists(path)
            ? Directory.EnumerateFiles(path)
                .Where(IsSupportedVideoFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

    private static bool ContainsSupportedVideoFile(string path)
        => Directory.EnumerateFiles(path).Any(IsSupportedVideoFile);

    private static bool IsSupportedVideoFile(string path)
        => SupportedVideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
