using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Persistence;

public class SettingsService : ISettingsService, IDisposable
{
    private static readonly Logger Log = AppLog.For<SettingsService>();
    private static readonly TimeSpan DeferredSaveDelay = TimeSpan.FromMilliseconds(800);

    private readonly string settingsPath;
    private readonly object _sync = new();
    private AppSettings? settings;
    private CancellationTokenSource? _deferredSaveCts;
    private Task? _deferredSaveTask;
    private bool _isDisposed;

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
        lock (_sync)
        {
            settings = null;
        }
    }

    public AppSettings Load()
    {
        lock (_sync)
        {
            if (settings != null)
            {
                return settings;
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        AppSettings loaded;
        try
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                loaded = new AppSettings();
                Log.Info($"Load: file missing, created empty settings, elapsed {sw.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            loaded = new AppSettings();
            Log.Error("Load exception", ex);
        }

        lock (_sync)
        {
            settings = loaded;
            return settings;
        }
    }

    public void Save()
    {
        CancelDeferredSave();
        SaveCore();
    }

    private void SaveCore()
    {
        string? json = null;

        lock (_sync)
        {
            if (settings == null)
            {
                return;
            }

            json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }

        try
        {
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
            SaveCore();
            return (true, null);
        }
        catch (Exception ex)
        {
            Log.Error("AddFolder save failed", ex);
            return (false, ex.Message);
        }
    }

    public (List<string> AddedPaths, int Skipped) AddFoldersBatch(List<(string Path, string Name)> folders)
    {
        Log.Info($"AddFoldersBatch: received {folders.Count} candidates");
        var current = Load();
        var addedPaths = new List<string>();
        int skipped = 0;
        int maxOrder = current.Folders.Count > 0 ? current.Folders.Max(f => f.OrderIndex) : -1;

        foreach (var (path, name) in folders)
        {
            if (current.Folders.Exists(f => f.Path == path))
            {
                skipped++;
                continue;
            }
            current.Folders.Add(new FolderInfo
            {
                Path = path,
                Name = name,
                AddedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OrderIndex = ++maxOrder
            });
            addedPaths.Add(path);
        }

        Save();
        Log.Info($"AddFoldersBatch: added {addedPaths.Count}, skipped {skipped} duplicates");
        return (addedPaths, skipped);
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
        ScheduleDeferredSave();
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
        ScheduleDeferredSave();
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
        ScheduleDeferredSave();
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

    private void ScheduleDeferredSave()
    {
        if (_isDisposed)
            return;

        CancellationTokenSource? previousCts;
        CancellationTokenSource currentCts;

        lock (_sync)
        {
            previousCts = _deferredSaveCts;
            _deferredSaveCts = new CancellationTokenSource();
            currentCts = _deferredSaveCts;
            _deferredSaveTask = DeferredSaveAsync(currentCts.Token);
        }

        previousCts?.Cancel();
        previousCts?.Dispose();
    }

    private async Task DeferredSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DeferredSaveDelay, cancellationToken);
            SaveCore();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelDeferredSave()
    {
        CancellationTokenSource? cts;

        lock (_sync)
        {
            cts = _deferredSaveCts;
            _deferredSaveCts = null;
            _deferredSaveTask = null;
        }

        cts?.Cancel();
        cts?.Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        CancelDeferredSave();
        SaveCore();
    }

}



