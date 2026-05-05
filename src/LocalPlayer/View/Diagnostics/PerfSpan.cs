using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace LocalPlayer.View.Diagnostics;

public sealed class PerfSpan : IDisposable
{
    private static readonly PerfSpan Noop = new();

    private readonly long _allocatedBytesStart;
    private readonly int _gen0Start;
    private readonly int _gen1Start;
    private readonly int _gen2Start;
    private readonly long _startedTimestamp;
    private readonly DateTimeOffset _startedAtUtc;
    private PerfSpanReport? _report;

    private PerfSpan()
    {
        SpanName = string.Empty;
        Tags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));
    }

    private PerfSpan(string spanName, IReadOnlyDictionary<string, string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(spanName))
            throw new ArgumentException("Span name is required.", nameof(spanName));

        SpanName = spanName;
        Tags = tags != null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(tags))
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        _allocatedBytesStart = GC.GetTotalAllocatedBytes(false);
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
        _startedTimestamp = Stopwatch.GetTimestamp();
        _startedAtUtc = DateTimeOffset.UtcNow;
    }

    public string SpanName { get; }
    public IReadOnlyDictionary<string, string> Tags { get; }

    public static PerfSpan Begin(string spanName, IReadOnlyDictionary<string, string>? tags = null)
    {
        if (!PerfLogger.Enabled)
            return Noop;
        return new PerfSpan(spanName, tags);
    }

    public PerfSpanReport Stop()
    {
        if (_report != null)
            return _report;

        long endedTimestamp = Stopwatch.GetTimestamp();
        var endedAtUtc = DateTimeOffset.UtcNow;

        _report = new PerfSpanReport
        {
            SpanName = SpanName,
            StartedAtUtc = _startedAtUtc,
            EndedAtUtc = endedAtUtc,
            DurationMs = (endedTimestamp - _startedTimestamp) * 1000.0 / Stopwatch.Frequency,
            AllocatedBytes = GC.GetTotalAllocatedBytes(false) - _allocatedBytesStart,
            Gen0Collections = GC.CollectionCount(0) - _gen0Start,
            Gen1Collections = GC.CollectionCount(1) - _gen1Start,
            Gen2Collections = GC.CollectionCount(2) - _gen2Start,
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
