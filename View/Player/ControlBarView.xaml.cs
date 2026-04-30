using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Player;

public partial class ControlBarView : System.Windows.Controls.UserControl
{
    public ControlBarView()
    {
        InitializeComponent();
    }

    private PlayerViewModel? GetVm()
        => DataContext as PlayerViewModel;

    // --- Progress slider ---
    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var vm = GetVm();
        if (vm != null) vm.IsSeeking = true;

        if (e.OriginalSource is not Thumb && ProgressSlider.ActualWidth > 0)
        {
            double ratio;
            if (ProgressSlider.Template.FindName("PART_Track", ProgressSlider) is Track track && track.ActualWidth > 0)
            {
                Point trackPos = e.GetPosition(track);
                ratio = Math.Max(0, Math.Min(1, trackPos.X / track.ActualWidth));
            }
            else
            {
                Point pos = e.GetPosition(ProgressSlider);
                ratio = pos.X / ProgressSlider.ActualWidth;
            }
            double newValue = ProgressSlider.Minimum + ratio * (ProgressSlider.Maximum - ProgressSlider.Minimum);
            ProgressSlider.Value = Math.Max(ProgressSlider.Minimum, Math.Min(ProgressSlider.Maximum, newValue));
        }
    }

    private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var vm = GetVm();
        if (vm != null)
        {
            vm.IsSeeking = false;
            vm.SeekCommand.Execute((long)ProgressSlider.Value);
        }
    }

    private void ProgressSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        var vm = GetVm();
        if (vm != null)
        {
            vm.IsSeeking = false;
            vm.SeekCommand.Execute((long)ProgressSlider.Value);
        }
    }

    // --- Speed popup hover forwarding ---
    private void SpeedBtn_MouseEnter(object sender, MouseEventArgs e)
        => GetVm()?.OnSpeedEnter();

    private void SpeedBtn_MouseLeave(object sender, MouseEventArgs e)
        => GetVm()?.OnSpeedLeave();

    private void SpeedPopup_MouseEnter(object sender, MouseEventArgs e)
        => GetVm()?.OnSpeedEnter();

    private void SpeedPopup_MouseLeave(object sender, MouseEventArgs e)
        => GetVm()?.OnSpeedLeave();

    // --- Thumbnail preview forwarding ---
    private void ProgressSlider_MouseEnter(object sender, MouseEventArgs e)
        => GetVm()?.OnThumbnailEnter();

    private void ProgressSlider_MouseLeave(object sender, MouseEventArgs e)
        => GetVm()?.OnThumbnailLeave();

    private void ProgressSlider_MouseMove(object sender, MouseEventArgs e)
        => GetVm()?.OnThumbnailMove(e.GetPosition(ProgressSlider), ProgressSlider.ActualWidth);

    private void ProgressPopup_MouseEnter(object sender, MouseEventArgs e)
        => GetVm()?.OnThumbnailPopupEnter();

    private void ProgressPopup_MouseLeave(object sender, MouseEventArgs e)
        => GetVm()?.OnThumbnailPopupLeave();

    // --- Keyboard forwarding ---
    private void RootGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = GetVm();
        if (vm?.HandleKeyDown(e) == true)
            e.Handled = true;
    }

    private void RootGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var vm = GetVm();
        if (vm?.HandleKeyDown(e) == true)
            e.Handled = true;
    }
}
