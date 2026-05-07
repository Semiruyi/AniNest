using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

namespace AniNest.Infrastructure.Diagnostics;

public sealed class FrameTimingCollector : IDisposable
{
    private readonly object _gate = new();
    private readonly FrameSampleBuffer _buffer;
    private readonly List<JankFrame> _jankFrames = new();
    private readonly double _jankThresholdMs;
    private long _firstTimestamp;
    private long _lastTimestamp;
    private bool _isRunning;

    public FrameTimingCollector(int capacity = 32_768, double jankThresholdMs = 6.25)
    {
        _buffer = new FrameSampleBuffer(capacity);
        _jankThresholdMs = jankThresholdMs;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning;
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_isRunning)
                return;

            _buffer.Clear();
            _jankFrames.Clear();
            _firstTimestamp = 0;
            _lastTimestamp = 0;
            CompositionTarget.Rendering += OnRendering;
            _isRunning = true;
        }
    }

    public FrameTimingSnapshot Stop()
    {
        lock (_gate)
        {
            if (_isRunning)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isRunning = false;
            }

            return CreateSnapshot();
        }
    }

    public FrameTimingSnapshot Snapshot()
    {
        lock (_gate)
            return CreateSnapshot();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (!_isRunning)
                return;

            CompositionTarget.Rendering -= OnRendering;
            _isRunning = false;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            long now = Stopwatch.GetTimestamp();
            if (_firstTimestamp == 0)
                _firstTimestamp = now;

            if (_lastTimestamp != 0)
            {
                double frameTimeMs = (now - _lastTimestamp) * 1000.0 / Stopwatch.Frequency;
                _buffer.Add(frameTimeMs);

                if (frameTimeMs > _jankThresholdMs)
                {
                    double offsetMs = (now - _firstTimestamp) * 1000.0 / Stopwatch.Frequency;
                    _jankFrames.Add(new JankFrame(offsetMs, frameTimeMs));
                }
            }

            _lastTimestamp = now;
        }
    }

    private FrameTimingSnapshot CreateSnapshot()
        => new(_buffer.SnapshotOrdered(), _buffer.DroppedSamples, RenderCapability.Tier >> 16, _jankFrames.ToArray());
}
