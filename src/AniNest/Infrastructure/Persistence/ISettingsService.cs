using System.Collections.Generic;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Persistence;

public interface ISettingsService
{
    void Reload();
    AppSettings Load();
    void Save();

    (bool Success, string? Error) AddFolder(string path, string name);
    (List<string> AddedPaths, int Skipped) AddFoldersBatch(List<(string Path, string Name)> folders);
    void RemoveFolder(string path);
    List<FolderInfo> GetFolders();
    void ReorderFolders(List<string> orderedPaths);

    VideoProgress? GetVideoProgress(string filePath);
    void SetVideoProgress(string filePath, long position, long duration);
    void MarkVideoPlayed(string filePath);
    bool IsVideoPlayed(string filePath);
    FolderProgress? GetFolderProgress(string folderPath);
    void SetFolderProgress(string folderPath, string lastVideoPath);
    double GetFolderPlayedPercent(string folderPath, string[] videoFiles);

    int GetThumbnailExpiryDays();
    void SetThumbnailExpiryDays(int days);

}



