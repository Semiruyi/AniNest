using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.Infrastructure.Diagnostics;
using Size = System.Windows.Size;

namespace LocalPlayer.Presentation.Primitives;

public interface ITransitioningContentLifecycle
{
    void OnAppearing();
    void OnDisappearing();
}

public class TransitioningContentControl : ContentControl
{
    private readonly Grid _root = new();
    private ContentPresenter _activePresenter = new();
    private ContentPresenter _inactivePresenter = new();
    private bool _isTransitioning;
    private PerfSceneSession? _transitionScene;

    public event EventHandler? TransitionCompleted;

    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(nameof(TransitionDuration), typeof(int), typeof(TransitioningContentControl),
            new PropertyMetadata(500));

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
        _inactivePresenter.Opacity = 0;
        _inactivePresenter.IsHitTestVisible = false;

        _root.Children.Add(_activePresenter);
        _root.Children.Add(_inactivePresenter);
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

        if (ReferenceEquals(oldContent, newContent))
            return;

        if (oldContent is ITransitioningContentLifecycle oldLifecycle)
            oldLifecycle.OnDisappearing();

        if (newContent == null)
        {
            _activePresenter.Content = null;
            _inactivePresenter.Content = null;
            return;
        }

        if (oldContent == null)
        {
            _activePresenter.Content = newContent;
            return;
        }

        if (newContent is ITransitioningContentLifecycle newLifecycle)
            newLifecycle.OnAppearing();

        if (_isTransitioning)
            AbortTransition();

        StartTransition(newContent);
    }

    private void StartTransition(object newContent)
    {
        _isTransitioning = true;
        IsTransitioning = true;

        string fromName = GetContentName(_activePresenter.Content);
        string toName = GetContentName(newContent);
        var tags = new Dictionary<string, string>
        {
            ["from"] = fromName,
            ["to"] = toName
        };

        using var setupSpan = PerfSpan.Begin($"PageTransition.Setup.{fromName}->{toName}", tags);
        _transitionScene?.Dispose();
        _transitionScene = PerfScenes.Begin($"PageTransition.Render.{fromName}->{toName}", tags);

        _activePresenter.BeginAnimation(OpacityProperty, null);
        _inactivePresenter.BeginAnimation(OpacityProperty, null);

        using (PerfSpan.Begin($"PageTransition.AttachContent.{fromName}->{toName}", tags))
        {
            _inactivePresenter.Content = newContent;
            _inactivePresenter.IsHitTestVisible = true;
        }

        using (PerfSpan.Begin($"PageTransition.Layout.{fromName}->{toName}", tags))
        {
            _inactivePresenter.InvalidateMeasure();
            _inactivePresenter.InvalidateArrange();
            _root.InvalidateMeasure();
            _root.InvalidateArrange();
        }

        int fadeMs = TransitionDuration > 0 ? TransitionDuration : 160;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(fadeMs))
        {
            EasingFunction = ease
        };

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMs))
        {
            EasingFunction = ease
        };

        fadeIn.Completed += (_, _) => FinishTransition();

        using (PerfSpan.Begin($"PageTransition.BeginAnimations.{fromName}->{toName}", tags))
        {
            _activePresenter.BeginAnimation(OpacityProperty, fadeOut);
            _inactivePresenter.BeginAnimation(OpacityProperty, fadeIn);
        }
    }

    private void FinishTransition()
    {
        // Swap so _activePresenter always holds the visible content.
        var temp = _activePresenter;
        _activePresenter = _inactivePresenter;
        _inactivePresenter = temp;

        _inactivePresenter.Content = null;
        _inactivePresenter.Opacity = 0;
        _inactivePresenter.IsHitTestVisible = false;
        _inactivePresenter.BeginAnimation(OpacityProperty, null);

        _activePresenter.Opacity = 1;
        _activePresenter.BeginAnimation(OpacityProperty, null);

        _isTransitioning = false;
        IsTransitioning = false;
        _transitionScene?.Stop();
        _transitionScene = null;
        TransitionCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void AbortTransition()
    {
        _activePresenter.BeginAnimation(OpacityProperty, null);
        _inactivePresenter.BeginAnimation(OpacityProperty, null);

        _inactivePresenter.Content = null;
        _inactivePresenter.Opacity = 0;
        _inactivePresenter.IsHitTestVisible = false;

        _activePresenter.Opacity = 1;
        _activePresenter.IsHitTestVisible = true;

        _isTransitioning = false;
        IsTransitioning = false;
        _transitionScene?.Stop();
        _transitionScene = null;
    }

    private static string GetContentName(object? content)
    {
        string? typeName = content?.GetType().Name;
        return string.IsNullOrWhiteSpace(typeName) ? "Null" : typeName;
    }
}

