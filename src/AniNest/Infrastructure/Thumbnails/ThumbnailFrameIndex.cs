using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailFrameIndex
{
    private const string FileName = "frames.json";
    private static readonly Logger Log = AppLog.For(nameof(ThumbnailFrameIndex));

    public static string GetIndexPath(string thumbnailDirectory)
        => Path.Combine(thumbnailDirectory, FileName);

    public static void Save(string thumbnailDirectory, IReadOnlyList<long> framePositionsMs)
    {
        string path = GetIndexPath(thumbnailDirectory);
        string json = JsonSerializer.Serialize(framePositionsMs);
        File.WriteAllText(path, json);
    }

    public static long[]? Load(string thumbnailDirectory)
    {
        string path = GetIndexPath(thumbnailDirectory);
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<long[]>(json)
                ?? JsonSerializer.Deserialize<int[]>(json)?.Select(static value => (long)value).ToArray();
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveThumbnailPath(string thumbnailDirectory, long requestedPositionMs)
    {
        int? frameIndex = ResolveFrameIndex(thumbnailDirectory, requestedPositionMs);
        if (frameIndex == null)
            return null;

        string resolvedPath = Path.Combine(thumbnailDirectory, $"{frameIndex.Value + 1:D4}.jpg");
        return File.Exists(resolvedPath) ? resolvedPath : null;
    }

    public static int? ResolveFrameIndex(string thumbnailDirectory, long requestedPositionMs)
    {
        long[]? framePositionsMs = Load(thumbnailDirectory);
        if (framePositionsMs == null || framePositionsMs.Length == 0)
        {
            int requestedSecond = requestedPositionMs <= 0
                ? 0
                : (int)(requestedPositionMs / 1000);
            string legacyPath = Path.Combine(thumbnailDirectory, $"{requestedSecond + 1:D4}.jpg");
            bool exists = File.Exists(legacyPath);
            if (!exists)
            {
                Log.Debug(
                    $"Thumbnail frame legacy fallback miss: dir={Path.GetFileName(thumbnailDirectory)}, requestedMs={requestedPositionMs}, requestedSecond={requestedSecond}, file={Path.GetFileName(legacyPath)}");
            }
            return exists ? requestedSecond : null;
        }

        return FindNearestFrameIndex(framePositionsMs, requestedPositionMs);
    }

    public static string? ResolveThumbnailPath(string thumbnailDirectory, int requestedSecond)
        => ResolveThumbnailPath(thumbnailDirectory, requestedSecond * 1000L);

    internal static int FindNearestFrameIndex(IReadOnlyList<long> framePositionsMs, long requestedPositionMs)
    {
        if (framePositionsMs.Count == 0)
            throw new ArgumentException("Frame index cannot be empty.", nameof(framePositionsMs));

        int insertionIndex = FindFirstGreaterThanOrEqual(framePositionsMs, requestedPositionMs);
        if (insertionIndex <= 0)
            return 0;

        if (insertionIndex >= framePositionsMs.Count)
            return framePositionsMs.Count - 1;

        int previousIndex = insertionIndex - 1;
        long previousDistance = Math.Abs(framePositionsMs[previousIndex] - requestedPositionMs);
        long nextDistance = Math.Abs(framePositionsMs[insertionIndex] - requestedPositionMs);
        return previousDistance <= nextDistance
            ? previousIndex
            : insertionIndex;
    }

    private static int FindFirstGreaterThanOrEqual(IReadOnlyList<long> framePositionsMs, long requestedPositionMs)
    {
        int low = 0;
        int high = framePositionsMs.Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            if (framePositionsMs[mid] < requestedPositionMs)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }
}
