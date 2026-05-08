using System.Collections.Generic;

namespace AniNest.Presentation.Overlays;

internal sealed class OverlayPointerDecision
{
    public required OverlayHitResult Hit { get; init; }
    public required OverlayCloseReason CloseReason { get; init; }
    public required OverlayPointerBehavior PointerBehavior { get; init; }
    public required IReadOnlyCollection<AnimatedOverlay> KeepSet { get; init; }
    public required IReadOnlyCollection<AnimatedOverlay> InterceptedKeepSet { get; init; }
    public required IReadOnlyCollection<AnimatedOverlay> CloseSet { get; init; }
}
