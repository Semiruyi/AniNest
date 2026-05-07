using System;

namespace AniNest.Presentation.Overlays;

[Flags]
public enum OverlayOutsidePassthroughTargets
{
    None = 0,
    TitleBarInteractive = 1 << 0,
    TitleBarDragZone = 1 << 1,
    ContentInteractive = 1 << 2,
    ContentBackground = 1 << 3,
}
