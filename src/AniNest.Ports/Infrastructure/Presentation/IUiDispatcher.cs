using System;

namespace AniNest.Infrastructure.Presentation;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Invoke(Action action);
    void BeginInvoke(Action action);
}
