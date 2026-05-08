namespace AniNest.Presentation.Overlays;

public enum OverlayInteractionPreset
{
    /// <summary>
    /// No preset. The overlay falls back to its raw close and pointer properties.
    /// </summary>
    None,

    /// <summary>
    /// Anchored menu behavior for title-bar style menus and submenus.
    /// </summary>
    MenuLike,

    /// <summary>
    /// Capture-oriented behavior for overlays that can temporarily reserve input.
    /// </summary>
    CaptureLike,

    /// <summary>
    /// Strict context-menu behavior that strongly owns the current interaction.
    /// </summary>
    ContextLike,

    /// <summary>
    /// Card context-menu behavior that supports right-click switching between items.
    /// </summary>
    CardContextLike,

    /// <summary>
    /// Lightweight tool-panel behavior for interactive utility surfaces such as speed panels.
    /// </summary>
    ToolPanelLike,
}
