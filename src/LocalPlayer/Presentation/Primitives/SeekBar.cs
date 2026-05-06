using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LocalPlayer.Features.Player;
using LocalPlayer.Presentation.Animations;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace LocalPlayer.Presentation.Primitives;

public class SeekBar : ContentControl
{
    private const string LogTag = "[SeekBar]";


    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(nameof(Position), typeof(double), typeof(SeekBar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPositionChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(SeekBar),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty BufferedPositionProperty =
        DependencyProperty.Register(nameof(BufferedPosition), typeof(double), typeof(SeekBar),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsSeekingProperty =
        DependencyProperty.Register(nameof(IsSeeking), typeof(bool), typeof(SeekBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SeekCommandProperty =
        DependencyProperty.Register(nameof(SeekCommand), typeof(ICommand), typeof(SeekBar));

    public static readonly DependencyProperty ThumbnailPreviewProperty =
        DependencyProperty.Register(nameof(ThumbnailPreview), typeof(ThumbnailPreviewController), typeof(SeekBar));

    public static readonly DependencyProperty ShowTooltipProperty =
        DependencyProperty.Register(nameof(ShowTooltip), typeof(bool), typeof(SeekBar),
            new PropertyMetadata(true));

    public static readonly RoutedEvent SeekCompletedEvent =
        EventManager.RegisterRoutedEvent(nameof(SeekCompleted), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(SeekBar));

    public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public double BufferedPosition { get => (double)GetValue(BufferedPositionProperty); set => SetValue(BufferedPositionProperty, value); }
    public bool IsSeeking { get => (bool)GetValue(IsSeekingProperty); set => SetValue(IsSeekingProperty, value); }
    public ICommand SeekCommand { get => (ICommand)GetValue(SeekCommandProperty); set => SetValue(SeekCommandProperty, value); }
    public ThumbnailPreviewController? ThumbnailPreview { get => (ThumbnailPreviewController?)GetValue(ThumbnailPreviewProperty); set => SetValue(ThumbnailPreviewProperty, value); }
    public bool ShowTooltip { get => (bool)GetValue(ShowTooltipProperty); set => SetValue(ShowTooltipProperty, value); }

    public event RoutedEventHandler SeekCompleted
    {
        add => AddHandler(SeekCompletedEvent, value);
        remove => RemoveHandler(SeekCompletedEvent, value);
    }


    private Grid? _rootGrid;
    private Border? _trackBg;
    private Rectangle? _bufferedRect;
    private Rectangle? _playedRect;
    private Canvas? _thumbCanvas;
    private Ellipse? _thumbShadow;
    private Ellipse? _thumbBg;
    private ScaleTransform? _thumbScale;
    private AnimatedPopup? _tooltip;
    private TextBlock? _tooltipText;


    private bool _isDragging;
    private bool _isMouseOver;
    private Point _lastMousePos;
    private long _seekTarget = -1;
    private bool _restoringPosition;


    private Brush _trackBgBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private Brush _bufferedBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
    private Brush _accentBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x7A, 0xFF));

    private double _trackHeight = 4;
    private double _thumbSize = 14;
    private double _thumbShadowSize = 16;


    public SeekBar()
    {
        Debug.WriteLine($"{LogTag} Constructor");
        Focusable = true;
        BuildVisualTree();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Debug.WriteLine($"{LogTag} Loaded, ActualWidth={ActualWidth:F0}, ActualHeight={ActualHeight:F0}");
        LoadResources();
        UpdateVisuals();
    }

    private void LoadResources()
    {
        if (TryFindResource("AccentBlue") is Brush ab) _accentBrush = ab;
        if (TryFindResource("SliderTrackBackground") is Brush tb) _trackBgBrush = tb;
        if (TryFindResource("SeekBarTrackHeight") is double th) _trackHeight = th;
        if (TryFindResource("SeekBarThumbSize") is double ts) _thumbSize = ts;
        if (TryFindResource("SeekBarThumbShadowSize") is double tss) _thumbShadowSize = tss;
    }

    private void BuildVisualTree()
    {
        _trackBg = new Border
        {
            Background = _trackBgBrush,
            Height = _trackHeight,
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Center
        };

        _bufferedRect = new Rectangle
        {
            Fill = _bufferedBrush,
            Height = _trackHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 0
        };

        _playedRect = new Rectangle
        {
            Fill = _accentBrush,
            Height = _trackHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 0,
            RadiusX = 2,
            RadiusY = 2
        };

        var trackContainer = new Grid { ClipToBounds = true };
        trackContainer.Children.Add(_trackBg);
        trackContainer.Children.Add(_bufferedRect);
        trackContainer.Children.Add(_playedRect);

        _thumbScale = new ScaleTransform(1, 1);

        _thumbShadow = new Ellipse
        {
            Fill = _accentBrush,
            Opacity = 0.25,
            Width = _thumbShadowSize,
            Height = _thumbShadowSize
        };

        _thumbBg = new Ellipse
        {
            Fill = Brushes.White,
            Width = _thumbSize,
            Height = _thumbSize
        };

        var thumbGrid = new Grid { RenderTransformOrigin = new Point(0.5, 0.5) };
        thumbGrid.RenderTransform = _thumbScale;
        thumbGrid.Children.Add(_thumbShadow);
        thumbGrid.Children.Add(_thumbBg);

        _thumbCanvas = new Canvas { ClipToBounds = false };
        _thumbCanvas.Children.Add(thumbGrid);

        // --- Simple text tooltip (time / percentage) ---
        _tooltipText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei, sans-serif")
        };

        _tooltip = new AnimatedPopup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Relative,
            AllowsTransparency = true,
            IsHitTestVisible = false,
            CloseOnOutsideClick = false,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x28, 0x28, 0x28)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = _tooltipText
            }
        };

        _rootGrid = new Grid { Height = 22, Background = Brushes.Transparent };
        _rootGrid.Children.Add(trackContainer);
        _rootGrid.Children.Add(_thumbCanvas);

        Content = _rootGrid;
    }


    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var result = base.ArrangeOverride(arrangeBounds);
        UpdateVisuals();
        return result;
    }


    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        Debug.WriteLine($"{LogTag} Down btn={e.ChangedButton} dur={Duration}");

        if (e.ChangedButton != MouseButton.Left || Duration <= 0) return;

        Focus();
        var pos = e.GetPosition(this);
        _isDragging = true;
        IsSeeking = true;
        HideTooltip();

        if (ActualWidth > 0)
        {
            double ratio = Math.Clamp(pos.X / ActualWidth, 0, 1);
            Position = ratio * Duration;
            Debug.WriteLine($"{LogTag}   seek ratio={ratio:F3} ms={Position}");
        }

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (Duration <= 0) return;

        _lastMousePos = e.GetPosition(this);

        if (_isDragging)
        {
            UpdateDragPosition();
        }
        else
        {
            UpdateTooltip();
        }

        ThumbnailPreview?.OnMove(_lastMousePos, ActualWidth);
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        Debug.WriteLine($"{LogTag} Up isDragging={_isDragging} btn={e.ChangedButton}");

        if (!_isDragging || e.ChangedButton != MouseButton.Left) return;
        ReleaseMouseCapture();
        FinishSeek();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Duration <= 0) return;
        long offset = e.Delta > 0 ? 5000 : -5000;
        long newPos = (long)Math.Clamp(Position + offset, 0, Duration);
        _seekTarget = newPos;
        Position = newPos;
        UpdateVisuals();
        ExecuteSeek(newPos);
        e.Handled = true;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _isMouseOver = true;
        if (!_isDragging)
            SetThumbScale(1.35, animate: true);
        ThumbnailPreview?.OnEnter();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _isMouseOver = false;
        SetThumbScale(1.0, animate: true);
        HideTooltip();
        ThumbnailPreview?.OnLeave();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        Debug.WriteLine($"{LogTag} LostCapture isDragging={_isDragging}");
        if (!_isDragging) return;
        FinishSeek();
    }


    private void UpdateDragPosition()
    {
        if (ActualWidth <= 0) return;
        double ratio = Math.Clamp(_lastMousePos.X / ActualWidth, 0, 1);
        Position = ratio * Duration;
        UpdateVisuals();
    }

    private void FinishSeek()
    {
        Debug.WriteLine($"{LogTag} FinishSeek");
        _isDragging = false;
        IsSeeking = false;
        _seekTarget = (long)Position;
        SetThumbScale(_isMouseOver ? 1.35 : 1.0);
        ExecuteSeek(_seekTarget);
    }

    private void ExecuteSeek(long timeMs)
    {
        var cmd = SeekCommand;
        Debug.WriteLine($"{LogTag} ExecuteSeek ms={timeMs} cmd={cmd}");
        if (cmd?.CanExecute(timeMs) == true)
            cmd.Execute(timeMs);
        RaiseEvent(new RoutedEventArgs(SeekCompletedEvent));
    }


    private void SetThumbScale(double s, bool animate = false, int durationMs = 200)
    {
        if (_thumbScale == null) return;
        if (animate)
        {
            AnimationHelper.AnimateScaleTransform(_thumbScale, s, durationMs, AnimationHelper.EaseOut);
        }
        else
        {
            _thumbScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _thumbScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _thumbScale.ScaleX = s;
            _thumbScale.ScaleY = s;
        }
    }

    private void UpdateTooltip()
    {
        if (!ShowTooltip) return;
        if (_tooltip == null || _tooltipText == null) return;
        if (ActualWidth <= 0) return;

        double ratio = Math.Clamp(_lastMousePos.X / ActualWidth, 0, 1);
        long hoverMs = (long)(ratio * Duration);
        _tooltipText.Text = FormatTime(hoverMs);

        _tooltip.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double popupW = _tooltip.Child.DesiredSize.Width;

        double x = _lastMousePos.X - popupW / 2;
        x = Math.Clamp(x, 0, ActualWidth - popupW);
        _tooltip.HorizontalOffset = x;
        _tooltip.VerticalOffset = -28;
        _tooltip.IsOpenAnimated = true;
    }

    private void HideTooltip()
    {
        if (_tooltip != null)
            _tooltip.IsOpenAnimated = false;
    }


    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sb = (SeekBar)d;
        if (sb._isDragging) return;

        if (sb._seekTarget >= 0)
        {
            double dist = Math.Abs((double)e.NewValue - sb._seekTarget);
            if (dist < 3000)
            {
                sb._seekTarget = -1;
                sb.IsSeeking = false;
                sb.UpdateVisuals();
            }
            else if (!sb._restoringPosition)
            {
                sb._restoringPosition = true;
                sb.Position = sb._seekTarget;
                sb._restoringPosition = false;
            }
            return;
        }

        sb.UpdateVisuals();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SeekBar)d).UpdateVisuals();
    }


    private int _updateCount;

    private void UpdateVisuals()
    {
        if (_playedRect == null || _bufferedRect == null || _thumbCanvas == null) return;

        double trackWidth = ActualWidth;
        if (trackWidth <= 0) return;

        _updateCount++;
        if (_updateCount <= 5 || _updateCount % 60 == 0)
            Debug.WriteLine($"{LogTag} UpdateVisuals #{_updateCount} w={trackWidth:F0} dur={Duration} pos={Position} buf={BufferedPosition}");

        if (Duration <= 0)
        {
            _playedRect.Width = 0;
            _bufferedRect.Width = 0;
            Canvas.SetLeft(_thumbCanvas.Children[0], 0);
            return;
        }

        double playedRatio = Math.Clamp(Position / Duration, 0, 1);
        double bufferedRatio = Math.Clamp(BufferedPosition / Duration, 0, 1);

        _playedRect.Width = playedRatio * trackWidth;
        _bufferedRect.Width = bufferedRatio * trackWidth;

        double thumbCenterX = playedRatio * trackWidth;
        double thumbLeft = thumbCenterX - _thumbSize / 2;
        var thumbGrid = _thumbCanvas.Children[0] as FrameworkElement;
        if (thumbGrid != null)
        {
            thumbGrid.Width = _thumbShadowSize;
            thumbGrid.Height = _thumbShadowSize;
            Canvas.SetLeft(thumbGrid, thumbLeft - (_thumbShadowSize - _thumbSize) / 2);
            Canvas.SetTop(thumbGrid, (22 - _thumbShadowSize) / 2);
        }
    }

    private static string FormatTime(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }
}

