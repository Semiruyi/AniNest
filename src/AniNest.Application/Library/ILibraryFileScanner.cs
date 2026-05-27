namespace AniNest.Application.Library;

public interface ILibraryFileScanner
{
    Task<bool> FolderExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<LibraryFolderScanResult> ScanFolderAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetVideoFilesAsync(string path, CancellationToken cancellationToken = default);
}
