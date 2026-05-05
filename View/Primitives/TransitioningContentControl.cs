using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalPlayer.View.Animations;
using LocalPlayer.View.Diagnostics;

namespace LocalPlayer.View.Primitives;

/// <summary>
/// Supports opacity transition between content changes without re-parenting.
/// </summary>
public class TransitioningContentControl : ContentControl
{
    private Grid _rootPanel = null!;
    private readonly ContentPresenter _presenterA = new();
    private readonly ContentPresenter _presenterB = new();
    private ContentPresenter _activePresenter = null!;
    private bool _isTransitioning;
    private PerfSceneSession? _transitionScene;

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
        _rootPanel = new Grid();
        _rootPanel.Children.Add(_presenterA);
        _rootPanel.Children.Add(_presenterB);
        _activePresenter = _presenterA;
        AddVisualChild(_rootPanel);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _rootPanel;

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _rootPanel.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _rootPanel.Measure(constraint);
        return _rootPanel.DesiredSize;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (oldContent == null || _rootPanel == null || newContent == null)
        {
            _activePresenter.Content = newContent;
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
        var fromName = GetContentName(_activePresenter.Content);
        var toName = GetContentName(newContent);
        var tags = new Dictionary<string, string>
        {
            ["from"] = fromName,
            ["to"] = toName
        };

        using var setupSpan = PerfSpan.Begin($"PageTransition.Setup.{fromName}->{toName}", tags);
        _transitionScene?.Dispose();
        _transitionScene = PerfScenes.Begin($"PageTransition.Render.{fromName}->{toName}", tags);

        var exitPresenter = _activePresenter;
        var enterPresenter = Other(exitPresenter);
        int duration = TransitionDuration;
        var ease = AnimationHelper.EaseInOut;

        Panel.SetZIndex(exitPresenter, 1);
        Panel.SetZIndex(enterPresenter, 0);

        enterPresenter.Content = newContent;
        enterPresenter.Visibility = Visibility.Visible;
        enterPresenter.Opacity = 0;

        AnimationHelper.Animate(exitPresenter, UIElement.OpacityProperty, 1, 0, duration, ease, () =>
        {
            exitPresenter.Content = null;
            exitPresenter.Visibility = Visibility.Collapsed;
            exitPresenter.Opacity = 1;
            Panel.SetZIndex(exitPresenter, 0);
            _isTransitioning = false;
            IsTransitioning = false;
            _transitionScene?.Stop();
            _transitionScene = null;
        });

        AnimationHelper.Animate(enterPresenter, UIElement.OpacityProperty, 0, 1, duration, ease);
        _activePresenter = enterPresenter;
    }

    private void AbortTransition()
    {
        _transitionScene?.Stop();
        _transitionScene = null;

        var inactive = Other(_activePresenter);

        foreach (var p in new ContentPresenter[] { _presenterA, _presenterB })
            p.BeginAnimation(UIElement.OpacityProperty, null);

        inactive.Content = null;
        inactive.Visibility = Visibility.Collapsed;
        inactive.Opacity = 1;
        Panel.SetZIndex(inactive, 0);

        _activePresenter.Opacity = 1;
        _isTransitioning = false;
        IsTransitioning = false;
    }

    private ContentPresenter Other(ContentPresenter p) => ReferenceEquals(p, _presenterA) ? _presenterB : _presenterA;

    private static string GetContentName(object? content)
    {
        var typeName = content?.GetType().Name;
        return string.IsNullOrWhiteSpace(typeName) ? "Null" : typeName;
    }
}
