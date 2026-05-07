using System.Threading.Tasks;

namespace AniNest.Infrastructure.Interop;

public class TaskbarAutoHideCoordinator : ITaskbarAutoHideCoordinator
{
    private bool? _savedTaskbarAutoHide;

    public async Task EnterPlayerPageAsync(string animationCode)
    {
        if (animationCode == "none")
            return;

        if (TaskbarHelper.IsAutoHideEnabled)
            return;

        _savedTaskbarAutoHide = false;
        await TaskbarHelper.EnableAutoHideAsync();
    }

    public async Task LeavePlayerPageAsync()
    {
        if (_savedTaskbarAutoHide is null)
            return;

        if (_savedTaskbarAutoHide == false)
            await TaskbarHelper.DisableAutoHideAsync();

        _savedTaskbarAutoHide = null;
    }

    public void RestoreIfNeeded()
    {
        if (_savedTaskbarAutoHide is not false)
            return;

        TaskbarHelper.DisableAutoHide();
        _savedTaskbarAutoHide = null;
    }
}
