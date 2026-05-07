using System.Collections.Generic;

namespace AniNest.Infrastructure.Diagnostics;

public sealed record FrameTimingSnapshot(
    double[] FrameTimesMs,
    long DroppedSamples,
    int RenderTier,
    IReadOnlyList<JankFrame> JankFrames);
