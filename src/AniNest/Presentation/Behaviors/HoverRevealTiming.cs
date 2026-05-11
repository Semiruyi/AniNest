using System;

namespace AniNest.Presentation.Behaviors;

public readonly record struct HoverRevealTiming(
    TimeSpan ShowDelay,
    TimeSpan HideDelay,
    TimeSpan MinVisibleDuration);
