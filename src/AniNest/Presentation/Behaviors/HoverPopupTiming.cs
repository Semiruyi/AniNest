using System;

namespace AniNest.Presentation.Behaviors;

public readonly record struct HoverPopupTiming(TimeSpan OpenDelay, TimeSpan CloseDelay);
