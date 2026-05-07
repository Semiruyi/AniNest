namespace LocalPlayer.Infrastructure.Thumbnails;

public interface IVideoScanner
{
    FolderScanResult ScanFolder(string folderPath);
    int CountVideosInFolder(string folderPath);
    string[] GetVideoFiles(string folderPath);
    string? FindCoverImage(string folderPath);
    List<string> FindVideoFolders(string rootPath);
}
