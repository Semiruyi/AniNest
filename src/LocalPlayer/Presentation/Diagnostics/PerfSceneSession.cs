using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace LocalPlayer.Presentation.Diagnostics;

public sealed class PerfSceneSession : IDisposable
{
    internal static readonly PerfSceneSession Noop = new();

    private readonly FrameTimingCollector? _collector;
    private readonly long _allocatedBytesStart;
    private readonly int _gen0Start;
    private readonly int _gen1Start;
    private readonly int _gen2Start;
    private readonly long _startedTimestamp;
    private readonly DateTimeOffset _startedAtUtc;
    private PerfSceneReport? _report;

    private PerfSceneSession()
    {
        SceneName = string.Empty;
        Tags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));
        _report = new PerfSceneReport
        {
            SceneName = string.Empty,
            StartedAtUtc = default,
            EndedAtUtc = default,
            DurationMs = 0,
            RenderTier = 0,
            AllocatedBytes = 0,
            Gen0Collections = 0,
            Gen1Collections = 0,
            Gen2Collections = 0,
            Statistics = FrameStatistics.Empty,
            Tags = Tags
        };
    }

    public PerfSceneSession(
        string sceneName,
        IReadOnlyDictionary<string, string>? tags = null,
        int sampleCapacity = 32_768)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Scene name is required.", nameof(sceneName));

        SceneName = sceneName;
        Tags = tags != null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(tags))
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        _collector = new FrameTimingCollector(sampleCapacity);
        _allocatedBytesStart = GC.GetTotalAllocatedBytes(false);
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
        _startedTimestamp = Stopwatch.GetTimestamp();
        _startedAtUtc = DateTimeOffset.UtcNow;

        _collector.Start();
    }

    public string SceneName { get; }
    public IReadOnlyDictionary<string, string> Tags { get; }
    public bool IsCompleted => _report != null;

    public PerfSceneReport Stop()
    {
        if (_report != null)
            return _report;

        FrameTimingSnapshot snapshot = _collector!.Stop();
        _collector.Dispose();

        long endedTimestamp = Stopwatch.GetTimestamp();
        var endedAtUtc = DateTimeOffset.UtcNow;
        var statistics = FrameStatistics.FromSamples(snapshot.FrameTimesMs, snapshot.DroppedSamples, snapshot.JankFrames);

        _report = new PerfSceneReport
        {
            SceneName = SceneName,
            StartedAtUtc = _startedAtUtc,
            EndedAtUtc = endedAtUtc,
            DurationMs = (endedTimestamp - _startedTimestamp) * 1000.0 / Stopwatch.Frequency,
            RenderTier = snapshot.RenderTier,
            AllocatedBytes = GC.GetTotalAllocatedBytes(false) - _allocatedBytesStart,
            Gen0Collections = GC.CollectionCount(0) - _gen0Start,
            Gen1Collections = GC.CollectionCount(1) - _gen1Start,
            Gen2Collections = GC.CollectionCount(2) - _gen2Start,
            Statistics = statistics,
            Tags = Tags
        };

        PerfLogger.Write(_report);
        return _report;
    }

    public void Dispose()
    {
        if (ReferenceEquals(this, Noop))
            return;
        Stop();
    }
}

