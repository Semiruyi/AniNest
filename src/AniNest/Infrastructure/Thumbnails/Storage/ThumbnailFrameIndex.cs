using System;
using System.Collections.Generic;
using System.Linq;

namespace AniNest.Infrastructure.Thumbnails;

internal static class ThumbnailFrameIndex
{
    public static long[]? Load(string thumbnailDirectory)
    {
        IReadOnlyList<long>? bundleFramePositions = ThumbnailBundle.ReadFramePositions(thumbnailDirectory);
        return bundleFramePositions is { Count: > 0 }
            ? bundleFramePositions.ToArray()
            : null;
    }

    public static int? ResolveFrameIndex(string thumbnailDirectory, long requestedPositionMs)
    {
        long[]? framePositionsMs = Load(thumbnailDirectory);
        if (framePositionsMs == null || framePositionsMs.Length == 0)
            return null;

        return FindNearestFrameIndex(framePositionsMs, requestedPositionMs);
    }

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
