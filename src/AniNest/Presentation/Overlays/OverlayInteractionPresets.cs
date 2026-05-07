namespace AniNest.Presentation.Overlays;

public static class OverlayInteractionPresets
{
    public static void ApplyMenuLike(AnimatedOverlay overlay)
    {
        Apply(
            overlay,
            leftAnchor: OverlayPointerBehavior.CloseAndPassThrough,
            rightAnchor: OverlayPointerBehavior.CloseAndPassThrough,
            outside: OverlayPointerBehavior.CloseAndConsume,
            passthroughTargets:
                OverlayOutsidePassthroughTargets.TitleBarInteractive |
                OverlayOutsidePassthroughTargets.TitleBarDragZone);
    }

    public static void ApplyCaptureLike(AnimatedOverlay overlay)
    {
        Apply(
            overlay,
            leftAnchor: OverlayPointerBehavior.CloseAndPassThrough,
            rightAnchor: OverlayPointerBehavior.CloseAndPassThrough,
            outside: OverlayPointerBehavior.CloseAndConsume,
            passthroughTargets:
                OverlayOutsidePassthroughTargets.TitleBarInteractive |
                OverlayOutsidePassthroughTargets.TitleBarDragZone);
    }

    public static void ApplyContextLike(AnimatedOverlay overlay)
    {
        Apply(
            overlay,
            leftAnchor: OverlayPointerBehavior.CloseAndConsume,
            rightAnchor: OverlayPointerBehavior.CloseAndConsume,
            outside: OverlayPointerBehavior.CloseAndConsume,
            passthroughTargets: OverlayOutsidePassthroughTargets.None);
    }

    private static void Apply(
        AnimatedOverlay overlay,
        OverlayPointerBehavior leftAnchor,
        OverlayPointerBehavior rightAnchor,
        OverlayPointerBehavior outside,
        OverlayOutsidePassthroughTargets passthroughTargets)
    {
        overlay.CloseOnOutsideClick = true;
        overlay.CloseOnEscape = true;
        overlay.ResetAnchorOnClose = true;
        overlay.LeftAnchorClickBehavior = leftAnchor;
        overlay.RightAnchorClickBehavior = rightAnchor;
        overlay.OutsidePointerBehavior = outside;
        overlay.OutsidePassthroughTargets = passthroughTargets;
    }
}
