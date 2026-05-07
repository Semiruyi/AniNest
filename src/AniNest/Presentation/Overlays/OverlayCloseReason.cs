namespace AniNest.Presentation.Overlays;

/// <summary>
/// Describes why an <see cref="AnimatedOverlay"/> closed so callers can
/// distinguish passive dismissal from explicit interaction-driven transitions.
/// </summary>
public enum OverlayCloseReason
{
    /// <summary>
    /// Closed by an explicit caller request.
    /// </summary>
    Programmatic,

    /// <summary>
    /// Closed because the pointer landed outside the active overlay chain.
    /// </summary>
    OutsideClick,

    /// <summary>
    /// Closed because the user pressed Escape.
    /// </summary>
    EscapeKey,

    /// <summary>
    /// Closed because the user interacted with the same anchor again.
    /// </summary>
    Toggle,

    /// <summary>
    /// Closed because interaction moved focus to another overlay chain.
    /// </summary>
    ChainSwitch,

    /// <summary>
    /// Closed because a parent overlay in the same chain was dismissed.
    /// </summary>
    ParentClosed,

    /// <summary>
    /// Closed because the anchor element was unloaded.
    /// </summary>
    AnchorUnavailable,

    /// <summary>
    /// Closed because the owning view was unloaded or switched away.
    /// </summary>
    ViewChanged,
}
