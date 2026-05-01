using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LocalPlayer.View.Behaviors;

public static class SeekSliderBehavior
{
    public static readonly DependencyProperty IsSeekingProperty =
        DependencyProperty.RegisterAttached("IsSeeking", typeof(bool), typeof(SeekSliderBehavior),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SeekCommandProperty =
        DependencyProperty.RegisterAttached("SeekCommand", typeof(ICommand), typeof(SeekSliderBehavior),
            new PropertyMetadata(null, OnPropertyChanged));

    public static bool GetIsSeeking(DependencyObject o) => (bool)o.GetValue(IsSeekingProperty);
    public static void SetIsSeeking(DependencyObject o, bool v) => o.SetValue(IsSeekingProperty, v);
    public static ICommand GetSeekCommand(DependencyObject o) => (ICommand)o.GetValue(SeekCommandProperty);
    public static void SetSeekCommand(DependencyObject o, ICommand v) => o.SetValue(SeekCommandProperty, v);

    private static readonly HashSet<Slider> _subscribed = new();

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Slider slider && _subscribed.Add(slider))
        {
            slider.PreviewMouseDown += (_, args) =>
            {
                if (args.ChangedButton != MouseButton.Left) return;
                slider.SetValue(IsSeekingProperty, true);
                if (args.OriginalSource is not Thumb && slider.ActualWidth > 0)
                {
                    double ratio;
                    if (slider.Template.FindName("PART_Track", slider) is Track track && track.ActualWidth > 0)
                    {
                        var trackPos = args.GetPosition(track);
                        ratio = Math.Max(0, Math.Min(1, trackPos.X / track.ActualWidth));
                    }
                    else
                    {
                        var pos = args.GetPosition(slider);
                        ratio = pos.X / slider.ActualWidth;
                    }
                    var newValue = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
                    slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
                }
            };
            slider.PreviewMouseUp += (_, args) =>
            {
                if (args.ChangedButton != MouseButton.Left) return;
                Seek(slider);
            };
            slider.LostMouseCapture += (_, _) => Seek(slider);
        }
    }

    private static void Seek(Slider slider)
    {
        slider.SetValue(IsSeekingProperty, false);
        var cmd = GetSeekCommand(slider);
        var val = (long)slider.Value;
        if (cmd?.CanExecute(val) == true)
            cmd.Execute(val);
    }
}
