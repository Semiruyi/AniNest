using System.Windows.Input;

namespace LocalPlayer.Features.Player.Input;

public interface IPlayerInputService
{
    bool TryHandlePreviewKeyDown(IPlayerInputHost host, KeyEventArgs args);
    bool TryHandlePreviewMouseDown(IPlayerInputHost host, MouseButtonEventArgs args);
    bool TryHandlePreviewMouseUp(IPlayerInputHost host, MouseButtonEventArgs args);
    bool TryHandlePreviewMouseWheel(IPlayerInputHost host, MouseWheelEventArgs args);
}
