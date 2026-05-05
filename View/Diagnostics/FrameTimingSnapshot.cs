namespace LocalPlayer.View.Diagnostics;

public sealed record FrameTimingSnapshot(
    double[] FrameTimesMs,
    long DroppedSamples,
    int RenderTier);
