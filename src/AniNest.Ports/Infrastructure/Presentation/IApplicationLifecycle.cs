using System;

namespace AniNest.Infrastructure.Presentation;

public interface IApplicationLifecycle
{
    event EventHandler? ExitRequested;
}
