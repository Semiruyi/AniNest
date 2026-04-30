using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using LocalPlayer.Model;

namespace LocalPlayer.Model;

public class SettingsService
{
    private static void Log(string message) => AppLog.Info(nameof(SettingsService), message);
    private static void LogDebug(string message) => AppLog.Debug(nameof(SettingsService), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(SettingsService), message, ex);

    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string settingsPath;
    private AppSettings? settings;

    private SettingsService()
    {
        // 便携模式：数据保存在程序所在目录的 Data 文件夹
        string appFolder = AppDomain.CurrentDomain.BaseDirectory;
        string dataFolder = Path.Combine(appFolder, "Data");

        if (!Directory.Exists(dataFolder))
            Directory.CreateDirectory(dataFolder);

        settingsPath = Path.Combine(dataFolder, "settings.json");

        Log($"SettingsService 创建, 路径: {settingsPath}");
    }

    public void Reload()
    {
        LogDebug("Reload: 清除缓存");
        settings = null;
    }

    public AppSettings Load()
    {
        if (settings != null)
        {
            LogDebug($"Load: 返回缓存, KeyBindings 数量: {settings.KeyBindings?.Count ?? 0}");
            return settings;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                LogDebug($"Load: 读取文件 {settingsPath}, 大小 {json.Length} 字节, 耗时 {sw.ElapsedMilliseconds}ms");
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                LogDebug($"Load: 反序列化完成, KeyBindings 数量: {settings.KeyBindings?.Count ?? 0}, 总耗时 {sw.ElapsedMilliseconds}ms");
                if (settings.KeyBindings != null)
                {
                    foreach (var kv in settings.KeyBindings)
                        LogDebug($"Load:   KeyBinding: {kv.Key} = {(Key)kv.Value} ({kv.Value})");
                }
            }
            else
            {
                settings = new AppSettings();
                Log($"Load: 文件不存在, 创建空配置, 耗时 {sw.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            settings = new AppSettings();
            LogError("Load 异常", ex);
        }

        return settings;
    }

    public void Save()
    {
        if (settings == null)
        {
            LogDebug("Save: settings 为 null, 跳过");
            return;
        }

        LogDebug($"Save: 开始保存, KeyBindings 数量: {settings.KeyBindings?.Count ?? 0}");
        if (settings.KeyBindings != null)
        {
            foreach (var kv in settings.KeyBindings)
                LogDebug($"Save:   KeyBinding: {kv.Key} = {(Key)kv.Value} ({kv.Value})");
        }

        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
            Log("配置保存成功");
        }
        catch (Exception ex)
        {
            LogError("Save 异常", ex);
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
        if (settings.VideoProgress.TryGetValue(filePath, out var existing) &&
            existing.Position == position && existing.Duration == duration)
            return;

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
        if (settings.VideoProgress.TryGetValue(filePath, out var existing) &&
            existing.IsPlayed)
            return;

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

    public int GetThumbnailExpiryDays()
    {
        return Load().ThumbnailExpiryDays;
    }

    public void SetThumbnailExpiryDays(int days)
    {
        var s = Load();
        s.ThumbnailExpiryDays = days;
        Save();
    }

    // ========== 键盘快捷键相关 ==========

    public static List<KeyBindingInfo> GetDefaultKeyBindings()
    {
        return new List<KeyBindingInfo>
        {
            new() { ActionName = "TogglePlayPause", DisplayName = "播放/暂停", DefaultKey = (int)Key.Space },
            new() { ActionName = "SeekBackward",  DisplayName = "快退 5 秒", DefaultKey = (int)Key.Left },
            new() { ActionName = "SeekForward",   DisplayName = "快进 5 秒", DefaultKey = (int)Key.Right },
            new() { ActionName = "ToggleFullscreen", DisplayName = "全屏", DefaultKey = (int)Key.F },
            new() { ActionName = "BackOrExitFullscreen", DisplayName = "退出全屏/返回", DefaultKey = (int)Key.Escape },
            new() { ActionName = "SeekBackwardAlt", DisplayName = "快退 5 秒 (备用)", DefaultKey = (int)Key.J },
            new() { ActionName = "SeekForwardAlt",  DisplayName = "快进 5 秒 (备用)", DefaultKey = (int)Key.L },
            new() { ActionName = "NextEpisode",     DisplayName = "下一集", DefaultKey = (int)Key.N },
            new() { ActionName = "PreviousEpisode", DisplayName = "上一集", DefaultKey = (int)Key.P },
        };
    }

    public Key GetKeyBinding(string actionName)
    {
        var s = Load();
        if (s.KeyBindings.TryGetValue(actionName, out int keyCode))
            return (Key)keyCode;
        var defaults = GetDefaultKeyBindings();
        var def = defaults.Find(d => d.ActionName == actionName);
        return def != null ? (Key)def.DefaultKey : Key.None;
    }

    public void SetKeyBinding(string actionName, Key key)
    {
        LogDebug($"SetKeyBinding: action={actionName}, key={key} ({(int)key})");
        var s = Load();
        s.KeyBindings ??= new Dictionary<string, int>();
        LogDebug($"SetKeyBinding: Load 后 KeyBindings 数量: {s.KeyBindings.Count}");
        s.KeyBindings[actionName] = (int)key;
        LogDebug($"SetKeyBinding: 设置后 KeyBindings 数量: {s.KeyBindings.Count}");
        Save();
    }

    public Dictionary<string, Key> GetAllKeyBindings()
    {
        var result = new Dictionary<string, Key>();
        var defaults = GetDefaultKeyBindings();
        var s = Load();
        s.KeyBindings ??= new Dictionary<string, int>();
        LogDebug($"GetAllKeyBindings: settings.KeyBindings 数量: {s.KeyBindings.Count}");
        foreach (var def in defaults)
        {
            var found = s.KeyBindings.TryGetValue(def.ActionName, out int keyCode);
            result[def.ActionName] = found ? (Key)keyCode : (Key)def.DefaultKey;
            LogDebug($"GetAllKeyBindings: {def.ActionName} = {(found ? ((Key)keyCode).ToString() : ((Key)def.DefaultKey).ToString())} ({(int)result[def.ActionName]}) [{(found ? "已保存" : "默认")}]");
        }
        return result;
    }
}
