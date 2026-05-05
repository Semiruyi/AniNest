using System.Collections.Generic;

namespace LocalPlayer.Presentation.Diagnostics;

public sealed record FrameTimingSnapshot(
    double[] FrameTimesMs,
    long DroppedSamples,
    int RenderTier,
    IReadOnlyList<JankFrame> JankFrames);

