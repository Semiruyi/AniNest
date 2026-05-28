using AniNest.Application.Library;

namespace AniNest.Host.Modules;

internal sealed class FileSystemLibraryFileScanner : ILibraryFileScanner
{
    private readonly FileSystemVideoFolderDiscovery _folderDiscovery;

    public FileSystemLibraryFileScanner(FileSystemVideoFolderDiscovery folderDiscovery)
    {
        _folderDiscovery = folderDiscovery;
    }

    public Task<bool> FolderExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(path));
    }

    public Task<LibraryFolderScanResult> ScanFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var videoCount = Directory.EnumerateFiles(path)
            .Count(FileSystemLibraryMediaRules.IsSupportedVideoFile);

        var coverPath = FileSystemLibraryMediaRules.SupportedCoverNames
            .Select(fileName => Path.Combine(path, fileName))
            .FirstOrDefault(File.Exists);

        return Task.FromResult(new LibraryFolderScanResult(videoCount, coverPath));
    }

    public Task<IReadOnlyList<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _folderDiscovery.FindDescendantVideoFolders(rootPath, cancellationToken));
    }

    public Task<IReadOnlyList<string>> GetVideoFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetVideoFiles(path));
    }

    public IReadOnlyList<string> GetVideoFiles(string path)
        => Directory.Exists(path)
            ? Directory.EnumerateFiles(path)
                .Where(FileSystemLibraryMediaRules.IsSupportedVideoFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
}
