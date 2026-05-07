using System;
using System.Collections.Generic;

namespace AniNest.Infrastructure.Diagnostics;

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
    private readonly string _sceneName;
    private readonly IReadOnlyDictionary<string, string> _tags;

    private PerfSceneSession()
    {
        _sceneName = string.Empty;
        _tags = new Dictionary<string, string>();
    }

    public PerfSceneSession(
        string sceneName,
        IReadOnlyDictionary<string, string>? tags = null,
        int sampleCapacity = 32_768)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Scene name is required.", nameof(sceneName));

        _sceneName = sceneName;
        _tags = tags != null
            ? new Dictionary<string, string>(tags)
            : new Dictionary<string, string>();

        _collector = new FrameTimingCollector(sampleCapacity);
        _collector.Start();
        _allocatedBytesStart = GC.GetTotalAllocatedBytes(false);
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
        _startedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        _startedAtUtc = DateTimeOffset.UtcNow;
    }

    public PerfSceneReport Stop()
    {
        if (_report != null)
            return _report;

        if (ReferenceEquals(this, Noop))
        {
            var noopEndedAtUtc = DateTimeOffset.UtcNow;
            _report = new PerfSceneReport
            {
                SceneName = _sceneName,
                StartedAtUtc = noopEndedAtUtc,
                EndedAtUtc = noopEndedAtUtc,
                DurationMs = 0,
                RenderTier = 0,
                AllocatedBytes = 0,
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0,
                Statistics = FrameStatistics.FromSamples([], droppedSamples: 0),
                Tags = _tags
            };

            return _report;
        }

        long endedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var endedAtUtc = DateTimeOffset.UtcNow;
        var snapshot = _collector!.Stop();

        _report = new PerfSceneReport
        {
            SceneName = _sceneName,
            StartedAtUtc = _startedAtUtc,
            EndedAtUtc = endedAtUtc,
            DurationMs = (endedTimestamp - _startedTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
            RenderTier = snapshot.RenderTier,
            AllocatedBytes = GC.GetTotalAllocatedBytes(false) - _allocatedBytesStart,
            Gen0Collections = GC.CollectionCount(0) - _gen0Start,
            Gen1Collections = GC.CollectionCount(1) - _gen1Start,
            Gen2Collections = GC.CollectionCount(2) - _gen2Start,
            Statistics = FrameStatistics.FromSamples(snapshot.FrameTimesMs, snapshot.DroppedSamples, snapshot.JankFrames),
            Tags = _tags
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
