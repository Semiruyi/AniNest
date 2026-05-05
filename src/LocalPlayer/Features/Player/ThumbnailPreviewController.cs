using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Infrastructure.Model;
using Point = System.Windows.Point;

namespace LocalPlayer.Features.Player;

public partial class ThumbnailPreviewController : ObservableObject
{
    private static readonly Logger Log = AppLog.For<ThumbnailPreviewController>();

    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly Func<string?> _getCurrentVideoPath;
    private readonly Func<long> _getMediaLength;

    private readonly Dictionary<int, BitmapSource> _thumbCache = new();
    private DispatcherTimer? _thumbShowTimer;
    private DispatcherTimer? _thumbHideTimer;
    private bool _thumbHovering;
    private bool _thumbVisible;
    private bool _thumbClosing;
    private int _lastRequestedSecond = -1;

    // ========== 缁戝畾灞炴€?==========

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private ImageSource? _imageSource;

    [ObservableProperty]
    private string _timeText = "";

    [ObservableProperty]
    private Visibility _imageVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private double _hOffset;

    public ThumbnailPreviewController(
        IThumbnailGenerator thumbnailGenerator,
        Func<string?> getCurrentVideoPath,
        Func<long> getMediaLength)
    {
        _thumbnailGenerator = thumbnailGenerator;
        _getCurrentVideoPath = getCurrentVideoPath;
        _getMediaLength = getMediaLength;
    }

    public void OnCurrentVideoPathChanged()
    {
        _thumbCache.Clear();
        _lastRequestedSecond = -1;
    }

    public void OnEnter()
    {
        _thumbHovering = true;
        _thumbHideTimer?.Stop();
        if (!_thumbVisible)
            (_thumbShowTimer ??= CreateShowTimer()).Start();
    }

    [RelayCommand]
    private void Enter() => OnEnter();

    public void OnLeave()
    {
        _thumbHovering = false;
        _thumbShowTimer?.Stop();
        (_thumbHideTimer ??= CreateHideTimer()).Start();
    }

    [RelayCommand]
    private void Leave() => OnLeave();

    public void OnPopupEnter()
    {
        _thumbHideTimer?.Stop();
    }

    [RelayCommand]
    private void PopupEnter() => OnPopupEnter();

    public void OnPopupLeave()
    {
        _thumbHideTimer?.Stop();
        (_thumbHideTimer ??= CreateHideTimer()).Start();
    }

    [RelayCommand]
    private void PopupLeave() => OnPopupLeave();

    public void OnMove(Point pos, double sliderWidth)
    {
        long length = _getMediaLength();
        if (length <= 0) return;

        double ratio = Math.Max(0, Math.Min(1, pos.X / sliderWidth));
        long hoverTimeMs = (long)(ratio * length);
        int hoverSecond = (int)(hoverTimeMs / 1000);

        TimeText = FormatTime(hoverTimeMs);
        double popupW = 160;
        // 姘村钩涓績涓嶈秴杩囪繘搴︽潯杈圭晫锛屽乏鍙冲悇鍙秴鍑轰竴鍗?
        HOffset = Math.Max(-popupW / 2, Math.Min(pos.X - popupW / 2, sliderWidth - popupW / 2));

        string? currentVideoPath = _getCurrentVideoPath();
        bool thumbReady = currentVideoPath != null &&
            _thumbnailGenerator.GetState(currentVideoPath) == ThumbnailState.Ready;
        ImageVisibility = thumbReady ? Visibility.Visible : Visibility.Collapsed;

        if (hoverSecond == _lastRequestedSecond) return;
        _lastRequestedSecond = hoverSecond;

        if (thumbReady && currentVideoPath != null)
        {
            if (_thumbCache.TryGetValue(hoverSecond, out var cached))
            {
                ImageSource = cached;
            }
            else
            {
                var bmp = LoadJpeg(currentVideoPath, hoverSecond);
                if (bmp != null)
                {
                    _thumbCache[hoverSecond] = bmp;
                    ImageSource = bmp;

                    if (_thumbCache.Count > 20)
                    {
                        var toRemove = _thumbCache.Keys.OrderBy(k => k).Take(_thumbCache.Count / 2).ToList();
                        foreach (var k in toRemove) _thumbCache.Remove(k);
                    }
                }
            }
        }
    }

    [RelayCommand]
    private void Move(MouseEventArgs e)
    {
        if (e.Source is FrameworkElement el)
            OnMove(e.GetPosition(el), el.ActualWidth);
    }

    public void Close()
    {
        _thumbHideTimer?.Stop();
        _thumbShowTimer?.Stop();
        IsOpen = false;
        _thumbVisible = false;
        _thumbHovering = false;
        _thumbClosing = false;
    }

    private void ShowThumbnail()
    {
        if (_thumbVisible || _thumbClosing) return;
        _thumbVisible = true;
        IsOpen = true;
    }

    private void HideThumbnail()
    {
        if (!_thumbVisible || _thumbClosing) return;
        _thumbClosing = true;
        _thumbVisible = false;
        _thumbClosing = false;
        IsOpen = false;
    }

    private DispatcherTimer CreateShowTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (!_thumbHovering) return;
            ShowThumbnail();
        };
        _thumbShowTimer = t;
        return t;
    }

    private DispatcherTimer CreateHideTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (_thumbHovering) return;
            HideThumbnail();
        };
        _thumbHideTimer = t;
        return t;
    }

    private BitmapSource? LoadJpeg(string videoPath, int second)
    {
        var path = _thumbnailGenerator.GetThumbnailPath(videoPath, second);
        if (path == null) return null;
        try
        {
            var decoder = new JpegBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            Log.Error($"缂╃暐鍥捐В鐮佸紓甯?second={second}", ex);
            return null;
        }
    }

    private static string FormatTime(long ms)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(ms);
        return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
    }
}

