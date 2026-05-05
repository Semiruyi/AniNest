using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
namespace LocalPlayer.Infrastructure.Persistence;

public class SettingsService : ISettingsService
{
    private static readonly Logger Log = AppLog.For<SettingsService>();

    private readonly string settingsPath;
    private AppSettings? settings;

    public SettingsService(string? customSettingsPath = null)
    {
        settingsPath = customSettingsPath ?? AppPaths.SettingsPath;
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        Log.Info($"SettingsService created, path: {settingsPath}");
    }

    public void Reload()
    {
        Log.Debug("Reload: clearing cache");
        settings = null;
    }

    public AppSettings Load()
    {
        if (settings != null)
        {
            Log.Debug($"Load: returning cached settings, KeyBindings count: {settings.KeyBindings?.Count ?? 0}");
            return settings;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                Log.Debug($"Load: read {settingsPath}, {json.Length} bytes, elapsed {sw.ElapsedMilliseconds}ms");
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                Log.Debug($"Load: deserialized, KeyBindings count: {settings.KeyBindings?.Count ?? 0}, elapsed {sw.ElapsedMilliseconds}ms");
                if (settings.KeyBindings != null)
                {
                    foreach (var kv in settings.KeyBindings)
                        Log.Debug($"Load:   KeyBinding: {kv.Key} = {(Key)kv.Value} ({kv.Value})");
                }
            }
            else
            {
                settings = new AppSettings();
                Log.Info($"Load: file missing, created empty settings, elapsed {sw.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            settings = new AppSettings();
            Log.Error("Load exception", ex);
        }

        return settings;
    }

    public void Save()
    {
        if (settings == null)
        {
            Log.Debug("Save: settings is null, skipped");
            return;
        }

        Log.Debug($"Save: begin, KeyBindings count: {settings.KeyBindings?.Count ?? 0}");
        if (settings.KeyBindings != null)
        {
            foreach (var kv in settings.KeyBindings)
                Log.Debug($"Save:   KeyBinding: {kv.Key} = {(Key)kv.Value} ({kv.Value})");
        }

        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
            Log.Info("settings saved");
        }
        catch (Exception ex)
        {
            Log.Error("Save exception", ex);
        }
    }

    public (bool Success, string? Error) AddFolder(string path, string name)
    {
        var current = Load();

        if (current.Folders.Exists(f => f.Path == path))
            return (false, "This folder has already been added.");

        int maxOrder = current.Folders.Count > 0 ? current.Folders.Max(f => f.OrderIndex) : -1;
        current.Folders.Add(new FolderInfo
        {
            Path = path,
            Name = name,
            AddedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            OrderIndex = maxOrder + 1
        });

        try
        {
            string json = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
            return (true, null);
        }
        catch (Exception ex)
        {
            Log.Error("AddFolder save failed", ex);
            return (false, ex.Message);
        }
    }

    public void RemoveFolder(string path)
    {
        var current = Load();
        current.Folders.RemoveAll(f => f.Path == path);
        Save();
    }

    public List<FolderInfo> GetFolders()
    {
        var folders = Load().Folders;
        for (int i = 0; i < folders.Count; i++)
        {
            if (folders[i].OrderIndex == 0 && i > 0)
                folders[i].OrderIndex = folders[i - 1].OrderIndex + 1;
        }
        return folders.OrderBy(f => f.OrderIndex).ThenBy(f => f.AddedTime).ToList();
    }

    public void ReorderFolders(List<string> orderedPaths)
    {
        var current = Load();
        for (int i = 0; i < orderedPaths.Count; i++)
        {
            var folder = current.Folders.FirstOrDefault(f => f.Path == orderedPaths[i]);
            if (folder != null)
                folder.OrderIndex = i;
        }
        Save();
    }

    public VideoProgress? GetVideoProgress(string filePath)
    {
        var current = Load();
        if (current.VideoProgress.TryGetValue(filePath, out var progress))
            return progress;
        return null;
    }

    public void SetVideoProgress(string filePath, long position, long duration)
    {
        var current = Load();
        if (current.VideoProgress.TryGetValue(filePath, out var existing) &&
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
        current.VideoProgress[filePath] = progress;
        Save();
    }

    public void MarkVideoPlayed(string filePath)
    {
        var current = Load();
        if (current.VideoProgress.TryGetValue(filePath, out var existing) &&
            existing.IsPlayed)
            return;

        if (!current.VideoProgress.TryGetValue(filePath, out var progress))
        {
            progress = new VideoProgress { FilePath = filePath };
            current.VideoProgress[filePath] = progress;
        }
        progress.IsPlayed = true;
        progress.LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }

    public bool IsVideoPlayed(string filePath)
    {
        var current = Load();
        return current.VideoProgress.TryGetValue(filePath, out var progress) && progress.IsPlayed;
    }

    public FolderProgress? GetFolderProgress(string folderPath)
    {
        var current = Load();
        if (current.FolderProgress.TryGetValue(folderPath, out var progress))
            return progress;
        return null;
    }

    public void SetFolderProgress(string folderPath, string lastVideoPath)
    {
        var current = Load();
        current.FolderProgress[folderPath] = new FolderProgress
        {
            FolderPath = folderPath,
            LastVideoPath = lastVideoPath,
            LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        Save();
    }

    public double GetFolderPlayedPercent(string folderPath, string[] videoFiles)
    {
        var current = Load();
        if (videoFiles.Length == 0) return 0;
        int playedCount = videoFiles.Count(f => current.VideoProgress.TryGetValue(f, out var p) && p.IsPlayed);
        return (double)playedCount / videoFiles.Length * 100;
    }

    public int GetThumbnailExpiryDays()
        => Load().ThumbnailExpiryDays;

    public void SetThumbnailExpiryDays(int days)
    {
        var current = Load();
        current.ThumbnailExpiryDays = days;
        Save();
    }

    public static List<KeyBindingInfo> GetDefaultKeyBindings()
    {
        return new List<KeyBindingInfo>
        {
            new() { ActionName = "TogglePlayPause", DisplayName = "Play/Pause", DefaultKey = (int)Key.Space },
            new() { ActionName = "SeekBackward", DisplayName = "Seek Back 5s", DefaultKey = (int)Key.Left },
            new() { ActionName = "SeekForward", DisplayName = "Seek Forward 5s", DefaultKey = (int)Key.Right },
            new() { ActionName = "Back", DisplayName = "Back", DefaultKey = (int)Key.Escape },
            new() { ActionName = "SeekBackwardAlt", DisplayName = "Seek Back 5s (Alt)", DefaultKey = (int)Key.J },
            new() { ActionName = "SeekForwardAlt", DisplayName = "Seek Forward 5s (Alt)", DefaultKey = (int)Key.L },
            new() { ActionName = "NextEpisode", DisplayName = "Next Episode", DefaultKey = (int)Key.N },
            new() { ActionName = "PreviousEpisode", DisplayName = "Previous Episode", DefaultKey = (int)Key.P },
        };
    }

    public Key GetKeyBinding(string actionName)
    {
        var current = Load();
        if (current.KeyBindings.TryGetValue(actionName, out int keyCode))
            return (Key)keyCode;

        var defaults = GetDefaultKeyBindings();
        var def = defaults.Find(d => d.ActionName == actionName);
        return def != null ? (Key)def.DefaultKey : Key.None;
    }

    public void SetKeyBinding(string actionName, Key key)
    {
        Log.Debug($"SetKeyBinding: action={actionName}, key={key} ({(int)key})");
        var current = Load();
        current.KeyBindings ??= new Dictionary<string, int>();
        current.KeyBindings[actionName] = (int)key;
        Save();
    }

    public Dictionary<string, Key> GetAllKeyBindings()
    {
        var result = new Dictionary<string, Key>();
        var defaults = GetDefaultKeyBindings();
        var current = Load();
        current.KeyBindings ??= new Dictionary<string, int>();

        foreach (var def in defaults)
        {
            var found = current.KeyBindings.TryGetValue(def.ActionName, out int keyCode);
            result[def.ActionName] = found ? (Key)keyCode : (Key)def.DefaultKey;
        }

        return result;
    }
}



