using System;

namespace AniNest.Infrastructure.Diagnostics;

public sealed class FrameSampleBuffer
{
    private readonly double[] _samples;
    private int _nextIndex;
    private int _count;

    public FrameSampleBuffer(int capacity = 32_768)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _samples = new double[capacity];
    }

    public int Capacity => _samples.Length;
    public int Count => _count;
    public long DroppedSamples { get; private set; }

    public void Add(double frameTimeMs)
    {
        if (!double.IsFinite(frameTimeMs) || frameTimeMs <= 0)
            return;

        if (_count == Capacity)
            DroppedSamples++;
        else
            _count++;

        _samples[_nextIndex] = frameTimeMs;
        _nextIndex = (_nextIndex + 1) % Capacity;
    }

    public double[] SnapshotOrdered()
    {
        if (_count == 0)
            return [];

        var result = new double[_count];
        int start = _count == Capacity ? _nextIndex : 0;

        for (int i = 0; i < _count; i++)
            result[i] = _samples[(start + i) % Capacity];

        return result;
    }

    public void Clear()
    {
        _nextIndex = 0;
        _count = 0;
        DroppedSamples = 0;
    }
}
