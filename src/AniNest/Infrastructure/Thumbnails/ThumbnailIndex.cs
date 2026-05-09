using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Thumbnails;

internal class ThumbnailEntryDto
{
    public string Md5 { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int TotalFrames { get; set; }
    public long MarkedForDeletionAt { get; set; }
}

internal static class ThumbnailIndex
{
    private static readonly Logger Log = AppLog.For(nameof(ThumbnailIndex));

    public static void Save(string indexPath, IReadOnlyCollection<ThumbnailTask> tasks)
    {
        var entries = new Dictionary<string, ThumbnailEntryDto>();
        foreach (var t in tasks)
        {
            entries[t.VideoPath] = new ThumbnailEntryDto
            {
                Md5 = t.Md5Dir,
                State = t.State.ToString(),
                TotalFrames = t.TotalFrames,
                MarkedForDeletionAt = t.MarkedForDeletionAt
            };
        }

        string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        string directory = Path.GetDirectoryName(indexPath) ?? AppPaths.ThumbnailDirectory;
        Directory.CreateDirectory(directory);

        string tempPath = indexPath + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(indexPath))
            File.Copy(tempPath, indexPath, overwrite: true);
        else
            File.Move(tempPath, indexPath);

        if (File.Exists(tempPath))
            File.Delete(tempPath);
    }

    public static List<ThumbnailTask> Load(string indexPath, string thumbBaseDir,
        HashSet<string> existingPaths)
    {
        var tasks = new List<ThumbnailTask>();

        if (!File.Exists(indexPath))
        {
            return tasks;
        }

        string json = File.ReadAllText(indexPath);
        var entries = JsonSerializer.Deserialize<Dictionary<string, ThumbnailEntryDto>>(json);
        if (entries == null) return tasks;

        foreach (var kv in entries)
        {
            if (existingPaths.Contains(kv.Key)) continue;

            var state = kv.Value.State switch
            {
                "Ready" => ThumbnailState.Ready,
                "Generating" => ThumbnailState.Pending,
                "Failed" => ThumbnailState.Pending,
                _ => ThumbnailState.Pending
            };

            string md5Dir = kv.Value.Md5;
            string fullDir = Path.Combine(thumbBaseDir, md5Dir);
            bool hasBundle = ThumbnailBundle.Exists(fullDir);
            int jpgCount = Directory.Exists(fullDir)
                ? Directory.GetFiles(fullDir, "*.jpg").Length
                : 0;

            if (Directory.Exists(fullDir) && state != ThumbnailState.Ready)
            {
                if (jpgCount > 0 || hasBundle)
                {
                    state = ThumbnailState.Ready;
                    Log.Info(
                        $"Thumbnail directory already contains cached thumbnails; marked Ready: {Path.GetFileName(kv.Key)}");
                }
            }
            else if (state == ThumbnailState.Ready && !Directory.Exists(fullDir))
            {
                state = ThumbnailState.Pending;
                Log.Info(
                    $"Thumbnail directory missing; reset to Pending: {kv.Key}");
            }

            int totalFrames = state == ThumbnailState.Ready
                ? (kv.Value.TotalFrames > 0
                    ? kv.Value.TotalFrames
                    : jpgCount)
                : 0;

            tasks.Add(new ThumbnailTask
            {
                VideoPath = kv.Key,
                Md5Dir = md5Dir,
                State = state,
                TotalFrames = totalFrames,
                Priority = int.MaxValue,
                MarkedForDeletionAt = kv.Value.MarkedForDeletionAt
            });
        }
        return tasks;
    }
}



