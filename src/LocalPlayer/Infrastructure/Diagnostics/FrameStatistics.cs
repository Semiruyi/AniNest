using System;
using System.Collections.Generic;
using System.Linq;

namespace AniNest.Infrastructure.Diagnostics;

public sealed record FrameStatistics
{
    public static FrameStatistics Empty { get; } = new();

    public int FrameCount { get; init; }
    public double AverageFrameTimeMs { get; init; }
    public double AverageFps { get; init; }
    public double MinFrameTimeMs { get; init; }
    public double MedianFrameTimeMs { get; init; }
    public double P95FrameTimeMs { get; init; }
    public double P99FrameTimeMs { get; init; }
    public double MaxFrameTimeMs { get; init; }
    public double OnePercentLowFps { get; init; }
    public int JankOver6_25MsCount { get; init; }
    public int JankOver8_33MsCount { get; init; }
    public int JankOver16_67MsCount { get; init; }
    public int JankOver33_33MsCount { get; init; }
    public long DroppedSamples { get; init; }
    public IReadOnlyList<JankFrame> JankFrames { get; init; } = Array.Empty<JankFrame>();

    public static FrameStatistics FromSamples(
        double[] frameTimesMs,
        long droppedSamples = 0,
        IReadOnlyList<JankFrame>? jankFrames = null)
    {
        ArgumentNullException.ThrowIfNull(frameTimesMs);

        var jankFramesOrEmpty = jankFrames ?? Array.Empty<JankFrame>();

        if (frameTimesMs.Length == 0)
            return Empty with { DroppedSamples = droppedSamples, JankFrames = jankFramesOrEmpty };

        var ordered = frameTimesMs.ToArray();
        Array.Sort(ordered);

        double averageFrameTimeMs = frameTimesMs.Average();

        return new FrameStatistics
        {
            FrameCount = frameTimesMs.Length,
            AverageFrameTimeMs = averageFrameTimeMs,
            AverageFps = ToFps(averageFrameTimeMs),
            MinFrameTimeMs = ordered[0],
            MedianFrameTimeMs = Percentile(ordered, 0.50),
            P95FrameTimeMs = Percentile(ordered, 0.95),
            P99FrameTimeMs = Percentile(ordered, 0.99),
            MaxFrameTimeMs = ordered[^1],
            OnePercentLowFps = CalculateOnePercentLowFps(ordered),
            JankOver6_25MsCount = CountOver(frameTimesMs, 6.25),
            JankOver8_33MsCount = CountOver(frameTimesMs, 8.33),
            JankOver16_67MsCount = CountOver(frameTimesMs, 16.67),
            JankOver33_33MsCount = CountOver(frameTimesMs, 33.33),
            DroppedSamples = droppedSamples,
            JankFrames = jankFramesOrEmpty
        };
    }

    private static double CalculateOnePercentLowFps(double[] orderedFrameTimesMs)
    {
        int count = orderedFrameTimesMs.Length;
        int sampleCount = Math.Max(1, (int)Math.Ceiling(count * 0.01));
        double total = 0;

        for (int i = count - sampleCount; i < count; i++)
            total += orderedFrameTimesMs[i];

        return ToFps(total / sampleCount);
    }

    private static int CountOver(double[] frameTimesMs, double thresholdMs)
    {
        int count = 0;
        foreach (double frameTimeMs in frameTimesMs)
        {
            if (frameTimeMs > thresholdMs)
                count++;
        }

        return count;
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
            return 0;

        if (sortedValues.Length == 1)
            return sortedValues[0];

        double position = percentile * (sortedValues.Length - 1);
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        double weight = position - lowerIndex;
        return sortedValues[lowerIndex] + ((sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight);
    }

    private static double ToFps(double frameTimeMs)
        => frameTimeMs <= 0 ? 0 : 1000.0 / frameTimeMs;
}
