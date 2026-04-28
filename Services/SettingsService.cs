using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LocalPlayer;
using LocalPlayer.Models;

namespace LocalPlayer.Services;

public class SettingsService
{
    private readonly string settingsPath;
    private AppSettings? settings;

    public SettingsService()
    {
        // 便携模式：数据保存在程序所在目录的 Data 文件夹
        string appFolder = AppDomain.CurrentDomain.BaseDirectory;
        string dataFolder = Path.Combine(appFolder, "Data");
        
        if (!Directory.Exists(dataFolder))
            Directory.CreateDirectory(dataFolder);
            
        settingsPath = Path.Combine(dataFolder, "settings.json");
        
        Console.WriteLine($"[Settings] 路径: {settingsPath}");
    }

    public void Reload()
    {
        settings = null;
    }

    public AppSettings Load()
    {
        if (settings != null)
            return settings;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                App.LogStartup($"SettingsService.Load 读取文件耗时 {sw.ElapsedMilliseconds}ms，大小 {json.Length} 字节");
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                App.LogStartup($"SettingsService.Load JSON 反序列化完成，总耗时 {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                settings = new AppSettings();
                App.LogStartup($"SettingsService.Load 文件不存在，创建空配置，耗时 {sw.ElapsedMilliseconds}ms");
            }
        }
        catch
        {
            settings = new AppSettings();
            App.LogStartup($"SettingsService.Load 异常，使用空配置，耗时 {sw.ElapsedMilliseconds}ms");
        }

        return settings;
    }

    public void Save()
    {
        if (settings == null) return;
        
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch
        {
            // 忽略保存错误
        }
    }

    public void AddFolder(string path, string name)
    {
        var settings = Load();
        
        if (!settings.Folders.Exists(f => f.Path == path))
        {
            int maxOrder = settings.Folders.Count > 0 ? settings.Folders.Max(f => f.OrderIndex) : -1;
            settings.Folders.Add(new FolderInfo
            {
                Path = path,
                Name = name,
                AddedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OrderIndex = maxOrder + 1
            });
            Save();
        }
    }

    public void RemoveFolder(string path)
    {
        var settings = Load();
        settings.Folders.RemoveAll(f => f.Path == path);
        Save();
    }

    public List<FolderInfo> GetFolders()
    {
        var folders = Load().Folders;
        // 兼容旧数据：没有 OrderIndex 的按 AddedTime 排序
        for (int i = 0; i < folders.Count; i++)
        {
            if (folders[i].OrderIndex == 0 && i > 0)
                folders[i].OrderIndex = folders[i - 1].OrderIndex + 1;
        }
        return folders.OrderBy(f => f.OrderIndex).ThenBy(f => f.AddedTime).ToList();
    }

    public void ReorderFolders(List<string> orderedPaths)
    {
        var settings = Load();
        for (int i = 0; i < orderedPaths.Count; i++)
        {
            var folder = settings.Folders.FirstOrDefault(f => f.Path == orderedPaths[i]);
            if (folder != null)
                folder.OrderIndex = i;
        }
        Save();
    }

    // ========== 播放进度相关 ==========

    public VideoProgress? GetVideoProgress(string filePath)
    {
        var settings = Load();
        if (settings.VideoProgress.TryGetValue(filePath, out var progress))
            return progress;
        return null;
    }

    public void SetVideoProgress(string filePath, long position, long duration)
    {
        var settings = Load();
        var progress = new VideoProgress
        {
            FilePath = filePath,
            Position = position,
            Duration = duration,
            IsPlayed = true,
            LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        settings.VideoProgress[filePath] = progress;
        Save();
    }

    public void MarkVideoPlayed(string filePath)
    {
        var settings = Load();
        if (!settings.VideoProgress.TryGetValue(filePath, out var progress))
        {
            progress = new VideoProgress { FilePath = filePath };
            settings.VideoProgress[filePath] = progress;
        }
        progress.IsPlayed = true;
        progress.LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }

    public bool IsVideoPlayed(string filePath)
    {
        var settings = Load();
        return settings.VideoProgress.TryGetValue(filePath, out var progress) && progress.IsPlayed;
    }

    public FolderProgress? GetFolderProgress(string folderPath)
    {
        var settings = Load();
        if (settings.FolderProgress.TryGetValue(folderPath, out var progress))
            return progress;
        return null;
    }

    public void SetFolderProgress(string folderPath, string lastVideoPath)
    {
        var settings = Load();
        settings.FolderProgress[folderPath] = new FolderProgress
        {
            FolderPath = folderPath,
            LastVideoPath = lastVideoPath,
            LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        Save();
    }

    public double GetFolderPlayedPercent(string folderPath, string[] videoFiles)
    {
        var settings = Load();
        if (videoFiles.Length == 0) return 0;
        int playedCount = videoFiles.Count(f => settings.VideoProgress.TryGetValue(f, out var p) && p.IsPlayed);
        return (double)playedCount / videoFiles.Length * 100;
    }
}
