namespace AniNest.Application.Library;

public interface ILibraryFileScanner
{
    Task<LibraryFolderScanResult> ScanFolderAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default);
}
