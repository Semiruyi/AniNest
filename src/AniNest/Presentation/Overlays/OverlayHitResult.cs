using System.Collections.Generic;

namespace AniNest.Presentation.Overlays;

public sealed class OverlayHitResult
{
    public OverlayHitKind Kind { get; init; }
    public OverlayOutsideHitKind OutsideKind { get; init; }
    public AnimatedOverlay? PrimaryOverlay { get; init; }
    public IReadOnlyList<AnimatedOverlay> OverlayPath { get; init; } = [];
}
