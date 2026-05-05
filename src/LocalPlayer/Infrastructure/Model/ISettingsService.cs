using System.Collections.Generic;
using System.Windows.Input;

namespace LocalPlayer.Infrastructure.Model;

public interface ISettingsService
{
    void Reload();
    AppSettings Load();
    void Save();

    (bool Success, string? Error) AddFolder(string path, string name);
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

    Key GetKeyBinding(string actionName);
    void SetKeyBinding(string actionName, Key key);
    Dictionary<string, Key> GetAllKeyBindings();
}

