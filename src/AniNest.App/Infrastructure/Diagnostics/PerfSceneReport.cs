using System;
using System.Collections.Generic;

namespace AniNest.Infrastructure.Diagnostics;

public sealed record PerfSceneReport
{
    public required string SceneName { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset EndedAtUtc { get; init; }
    public required double DurationMs { get; init; }
    public required int RenderTier { get; init; }
    public required long AllocatedBytes { get; init; }
    public required int Gen0Collections { get; init; }
    public required int Gen1Collections { get; init; }
    public required int Gen2Collections { get; init; }
    public required FrameStatistics Statistics { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
}
