using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailFrameIndex
{
    private const string FileName = "frames.json";

    public static string GetIndexPath(string thumbnailDirectory)
        => Path.Combine(thumbnailDirectory, FileName);

    public static void Save(string thumbnailDirectory, IReadOnlyList<int> frameSeconds)
    {
        string path = GetIndexPath(thumbnailDirectory);
        string json = JsonSerializer.Serialize(frameSeconds);
        File.WriteAllText(path, json);
    }

    public static int[]? Load(string thumbnailDirectory)
    {
        string path = GetIndexPath(thumbnailDirectory);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<int[]>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveThumbnailPath(string thumbnailDirectory, int requestedSecond)
    {
        int[]? frameSeconds = Load(thumbnailDirectory);
        if (frameSeconds == null || frameSeconds.Length == 0)
        {
            string legacyPath = Path.Combine(thumbnailDirectory, $"{requestedSecond + 1:D4}.jpg");
            return File.Exists(legacyPath) ? legacyPath : null;
        }

        int frameIndex = FindNearestFrameIndex(frameSeconds, requestedSecond);
        string resolvedPath = Path.Combine(thumbnailDirectory, $"{frameIndex + 1:D4}.jpg");
        return File.Exists(resolvedPath) ? resolvedPath : null;
    }

    internal static int FindNearestFrameIndex(IReadOnlyList<int> frameSeconds, int requestedSecond)
    {
        if (frameSeconds.Count == 0)
            throw new ArgumentException("Frame index cannot be empty.", nameof(frameSeconds));

        int insertionIndex = FindFirstGreaterThanOrEqual(frameSeconds, requestedSecond);
        if (insertionIndex <= 0)
            return 0;

        if (insertionIndex >= frameSeconds.Count)
            return frameSeconds.Count - 1;

        int previousIndex = insertionIndex - 1;
        int previousDistance = Math.Abs(frameSeconds[previousIndex] - requestedSecond);
        int nextDistance = Math.Abs(frameSeconds[insertionIndex] - requestedSecond);
        return previousDistance <= nextDistance
            ? previousIndex
            : insertionIndex;
    }

    private static int FindFirstGreaterThanOrEqual(IReadOnlyList<int> frameSeconds, int requestedSecond)
    {
        int low = 0;
        int high = frameSeconds.Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            if (frameSeconds[mid] < requestedSecond)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }
}
