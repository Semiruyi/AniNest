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
        File.WriteAllText(indexPath, json);
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

            if (Directory.Exists(fullDir) && state != ThumbnailState.Ready)
            {
                var files = Directory.GetFiles(fullDir, "*.jpg");
                if (files.Length > 0)
                {
                    state = ThumbnailState.Ready;
                    Log.Info(
                        $"纾佺洏鐩綍宸插瓨鍦?{files.Length} 甯э紝鏍囪 Ready: {Path.GetFileName(kv.Key)}");
                }
            }
            else if (state == ThumbnailState.Ready && !Directory.Exists(fullDir))
            {
                state = ThumbnailState.Pending;
                Log.Info(
                    $"鐩綍缂哄け锛岄噸缃负 Pending: {kv.Key}");
            }

            int totalFrames = state == ThumbnailState.Ready
                ? (Directory.Exists(fullDir)
                    ? Directory.GetFiles(fullDir, "*.jpg").Length
                    : kv.Value.TotalFrames)
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



