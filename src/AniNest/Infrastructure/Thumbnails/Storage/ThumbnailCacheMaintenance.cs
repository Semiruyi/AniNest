using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailCacheMaintenance
{
    private static readonly Logger Log = AppLog.For<ThumbnailCacheMaintenance>();

    private readonly ThumbnailIndexRepository _indexRepository;
    private readonly ISettingsService _settings;

    public ThumbnailCacheMaintenance(ThumbnailIndexRepository indexRepository, ISettingsService settings)
    {
        _indexRepository = indexRepository;
        _settings = settings;
    }

    public void CleanupTempArtifacts()
        => _indexRepository.CleanupTempArtifacts();

    public void LoadInto(ThumbnailTaskStore taskStore)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var loaded = _indexRepository.Load(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            taskStore.MergeLoadedTasks(loaded);

            sw.Stop();
            Log.Info($"Thumbnail index loaded: count={loaded.Count}, ready={taskStore.ReadyCount}, total={taskStore.TotalCount}");
        }
        catch (Exception ex)
        {
            Log.Error("Load tasks failed", ex);
            sw.Stop();
        }
    }

    public void SaveFrom(ThumbnailTaskStore taskStore)
    {
        try
        {
            _indexRepository.Save(taskStore.SnapshotTasks());
        }
        catch (Exception ex)
        {
            Log.Error("Save thumbnail index failed", ex);
        }
    }

    public bool CleanupExpired(ThumbnailTaskStore taskStore)
    {
        int expiryDays = _settings.GetThumbnailExpiryDays();
        if (expiryDays <= 0)
            return false;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long threshold = now - (long)expiryDays * 86400;
        List<ThumbnailTask> expired = taskStore.SnapshotTasks()
            .Where(t => t.MarkedForDeletionAt > 0 && t.MarkedForDeletionAt < threshold)
            .ToList();

        if (expired.Count == 0)
            return false;

        foreach (var task in expired)
            _indexRepository.DeleteTaskDirectory(task.Md5Dir);

        taskStore.RemoveTasks(expired);
        SaveFrom(taskStore);
        return true;
    }
}
