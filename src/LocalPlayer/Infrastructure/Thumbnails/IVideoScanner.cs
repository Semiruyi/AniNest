namespace LocalPlayer.Infrastructure.Thumbnails;

public interface IVideoScanner
{
    FolderScanResult ScanFolder(string folderPath);
    Task<FolderScanResult> ScanFolderAsync(string folderPath, CancellationToken cancellationToken = default);
    int CountVideosInFolder(string folderPath);
    Task<int> CountVideosInFolderAsync(string folderPath, CancellationToken cancellationToken = default);
    string[] GetVideoFiles(string folderPath);
    Task<string[]> GetVideoFilesAsync(string folderPath, CancellationToken cancellationToken = default);
    string? FindCoverImage(string folderPath);
    Task<string?> FindCoverImageAsync(string folderPath, CancellationToken cancellationToken = default);
    List<string> FindVideoFolders(string rootPath);
    Task<List<string>> FindVideoFoldersAsync(string rootPath, CancellationToken cancellationToken = default);
}
