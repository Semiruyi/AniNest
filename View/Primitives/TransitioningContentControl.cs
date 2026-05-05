using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.View.Diagnostics;

namespace LocalPlayer.View.Primitives;

public class TransitioningContentControl : ContentControl
{
    private readonly Grid _root = new();
    private readonly ContentPresenter _presenter = new();
    private readonly Border _veil = new();
    private bool _isTransitioning;
    private PerfSceneSession? _transitionScene;

    public event EventHandler? TransitionCompleted;

    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(nameof(TransitionDuration), typeof(int), typeof(TransitioningContentControl),
            new PropertyMetadata(160));

    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    private static readonly DependencyPropertyKey IsTransitioningPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsTransitioning), typeof(bool), typeof(TransitioningContentControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsTransitioningProperty = IsTransitioningPropertyKey.DependencyProperty;

    public bool IsTransitioning
    {
        get => (bool)GetValue(IsTransitioningProperty);
        private set => SetValue(IsTransitioningPropertyKey, value);
    }

    public TransitioningContentControl()
    {
        _veil.Visibility = Visibility.Collapsed;
        _veil.Opacity = 0;
        _veil.IsHitTestVisible = false;
        _veil.Background = BuildVeilBrush();

        _root.Children.Add(_presenter);
        _root.Children.Add(_veil);
        AddVisualChild(_root);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _root;

    protected override Size MeasureOverride(Size constraint)
    {
        _root.Measure(constraint);
        return _root.DesiredSize;
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (newContent == null)
        {
            _presenter.Content = null;
            return;
        }

        if (oldContent == null)
        {
            _presenter.Content = newContent;
            return;
        }

        if (_isTransitioning)
            AbortTransition();

        StartTransition(newContent);
    }

    private void StartTransition(object newContent)
    {
        _isTransitioning = true;
        IsTransitioning = true;

        string fromName = GetContentName(_presenter.Content);
        string toName = GetContentName(newContent);
        var tags = new Dictionary<string, string>
        {
            ["from"] = fromName,
            ["to"] = toName
        };

        using var setupSpan = PerfSpan.Begin($"PageTransition.Setup.{fromName}->{toName}", tags);
        _transitionScene?.Dispose();
        _transitionScene = PerfScenes.Begin($"PageTransition.Render.{fromName}->{toName}", tags);

        _veil.BeginAnimation(OpacityProperty, null);
        _presenter.BeginAnimation(OpacityProperty, null);

        _veil.Visibility = Visibility.Visible;
        _veil.Opacity = 0;
        _presenter.Opacity = 1;

        int coverMs = Math.Max(50, TransitionDuration / 2);
        int revealMs = Math.Max(70, TransitionDuration - coverMs);
        var coverEase = new CubicEase { EasingMode = EasingMode.EaseIn };
        var revealEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        var coverAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(coverMs))
        {
            EasingFunction = coverEase
        };

        coverAnim.Completed += (_, _) =>
        {
            _presenter.Content = newContent;
            _presenter.Opacity = 1;

            var revealVeil = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(revealMs))
            {
                EasingFunction = revealEase
            };
            revealVeil.Completed += (_, _) => FinishTransition();

            _veil.BeginAnimation(OpacityProperty, revealVeil);
        };

        _veil.BeginAnimation(OpacityProperty, coverAnim);
    }

    private void FinishTransition()
    {
        _veil.Visibility = Visibility.Collapsed;
        _veil.Opacity = 0;
        _presenter.Opacity = 1;
        _isTransitioning = false;
        IsTransitioning = false;
        _transitionScene?.Stop();
        _transitionScene = null;
        TransitionCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void AbortTransition()
    {
        _veil.BeginAnimation(OpacityProperty, null);
        _presenter.BeginAnimation(OpacityProperty, null);
        _veil.Visibility = Visibility.Collapsed;
        _veil.Opacity = 0;
        _presenter.Opacity = 1;
        _isTransitioning = false;
        IsTransitioning = false;
        _transitionScene?.Stop();
        _transitionScene = null;
    }

    private static Brush BuildVeilBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xF4, 8, 10, 12), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 10, 12, 15), 1));
        brush.Freeze();
        return brush;
    }

    private static string GetContentName(object? content)
    {
        string? typeName = content?.GetType().Name;
        return string.IsNullOrWhiteSpace(typeName) ? "Null" : typeName;
    }
}
