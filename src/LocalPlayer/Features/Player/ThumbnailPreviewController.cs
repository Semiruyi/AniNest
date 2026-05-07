using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Features.Player.Services;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Logging;
using Point = System.Windows.Point;

namespace LocalPlayer.Features.Player;

public partial class ThumbnailPreviewController : ObservableObject
{
    private static readonly Logger Log = AppLog.For<ThumbnailPreviewController>();

    private readonly IPlayerPlaybackFacade _playbackFacade;
    private readonly Func<string?> _getCurrentVideoPath;
    private readonly Func<long> _getMediaLength;

    private readonly Dictionary<int, BitmapSource> _thumbCache = new();
    private DispatcherTimer? _thumbShowTimer;
    private DispatcherTimer? _thumbHideTimer;
    private bool _thumbHovering;
    private bool _thumbVisible;
    private bool _thumbClosing;
    private int _lastRequestedSecond = -1;
    private int _lastLoadedSecond = -1;
    private CancellationTokenSource? _imageLoadCts;
    private int _imageLoadVersion;


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

    [ObservableProperty]
    private double _popupWidth = 160;

    [ObservableProperty]
    private double _popupVerticalOffset = -120;

    public ThumbnailPreviewController(
        IPlayerPlaybackFacade playbackFacade,
        Func<string?> getCurrentVideoPath,
        Func<long> getMediaLength)
    {
        _playbackFacade = playbackFacade;
        _getCurrentVideoPath = getCurrentVideoPath;
        _getMediaLength = getMediaLength;
    }

    public void OnCurrentVideoPathChanged()
    {
        CancelImageLoad();
        _thumbCache.Clear();
        _lastRequestedSecond = -1;
        _lastLoadedSecond = -1;
        ImageSource = null;
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
        string? currentVideoPath = _getCurrentVideoPath();
        bool thumbReady = currentVideoPath != null &&
            _playbackFacade.GetThumbnailState(currentVideoPath) == ThumbnailState.Ready;
        ImageVisibility = thumbReady ? Visibility.Visible : Visibility.Collapsed;
        if (!thumbReady)
            ImageSource = null;

        PopupWidth = thumbReady ? 160 : Math.Max(52, (TimeText.Length * 9) + 20);
        PopupVerticalOffset = thumbReady ? -120 : -28;
        HOffset = Math.Clamp(pos.X - (PopupWidth / 2), 0, Math.Max(0, sliderWidth - PopupWidth));

        if (hoverSecond == _lastRequestedSecond) return;
        _lastRequestedSecond = hoverSecond;

        if (thumbReady && currentVideoPath != null)
        {
            if (_thumbCache.TryGetValue(hoverSecond, out var cached))
            {
                _lastLoadedSecond = hoverSecond;
                ImageSource = cached;
            }
            else
            {
                _ = LoadJpegAsync(currentVideoPath, hoverSecond);
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
        CancelImageLoad();
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

    private async Task LoadJpegAsync(string videoPath, int second)
    {
        CancelImageLoad();
        _imageLoadCts = new CancellationTokenSource();
        var cancellationToken = _imageLoadCts.Token;
        int version = Interlocked.Increment(ref _imageLoadVersion);

        try
        {
            var bmp = await Task.Run(() => DecodeJpeg(videoPath, second, cancellationToken), cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            if (version != _imageLoadVersion)
                return;

            if (_getCurrentVideoPath() != videoPath || _lastRequestedSecond != second)
                return;

            if (bmp == null)
            {
                if (_lastLoadedSecond != second)
                    ImageSource = null;
                return;
            }

            _thumbCache[second] = bmp;
            _lastLoadedSecond = second;
            ImageSource = bmp;

            if (_thumbCache.Count > 20)
            {
                var toRemove = _thumbCache.Keys.OrderBy(k => k).Take(_thumbCache.Count / 2).ToList();
                foreach (var key in toRemove)
                    _thumbCache.Remove(key);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error("Load thumbnail failed", ex);
        }
    }

    private BitmapSource? DecodeJpeg(string videoPath, int second, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _playbackFacade.GetThumbnailPath(videoPath, second);
        if (path == null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        var decoder = new JpegBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private void CancelImageLoad()
    {
        _imageLoadCts?.Cancel();
        _imageLoadCts?.Dispose();
        _imageLoadCts = null;
    }

    private static string FormatTime(long ms)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(ms);
        return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
    }
}




