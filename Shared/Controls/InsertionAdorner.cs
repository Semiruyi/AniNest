using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.Shared.Controls;

public class InsertionAdorner : Adorner
{
    private bool _visible;
    private Rect _rect;

    private static readonly DependencyProperty AnimOpacityProperty =
        DependencyProperty.Register("AnimOpacity", typeof(double), typeof(InsertionAdorner),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private double AnimOpacity
    {
        get => (double)GetValue(AnimOpacityProperty);
        set => SetValue(AnimOpacityProperty, value);
    }

    private static readonly SolidColorBrush InsertionBrush = new(System.Windows.Media.Color.FromRgb(0x4A, 0x9E, 0xFF));

    public InsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public void ShowAt(double x, double y, double height)
    {
        _rect = new Rect(x, y, 4, height);

        if (!_visible)
        {
            _visible = true;
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(AnimOpacityProperty, anim);
        }

        InvalidateVisual();
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;

        var anim = new DoubleAnimation(AnimOpacity, 0, TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            var layer = AdornerLayer.GetAdornerLayer(AdornedElement);
            layer?.Remove(this);
        };
        BeginAnimation(AnimOpacityProperty, anim);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!_visible && AnimOpacity <= 0) return;
        drawingContext.PushOpacity(AnimOpacity);
        drawingContext.DrawRoundedRectangle(InsertionBrush, null, _rect, 2, 2);
        drawingContext.Pop();
    }
}
