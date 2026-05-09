using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Features.Player.Services;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Logging;
using AniNest.Presentation.Behaviors;
using Point = System.Windows.Point;

namespace AniNest.Features.Player;

public partial class ThumbnailPreviewController : ObservableObject
{
    private static readonly Logger Log = AppLog.For<ThumbnailPreviewController>();
    private static readonly HoverPopupTiming PreviewPopupTiming =
        new(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(150));

    private readonly IPlayerPlaybackFacade _playbackFacade;
    private readonly Func<string?> _getCurrentVideoPath;
    private readonly Func<long> _getMediaLength;
    private readonly HoverPopupController _hoverPopupController;

    private readonly Dictionary<long, BitmapSource> _thumbCache = new();
    private long _lastRequestedPositionMs = -1;
    private long _lastLoadedPositionMs = -1;
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
        _hoverPopupController = new HoverPopupController(
            PreviewPopupTiming,
            () => IsOpen,
            opened => IsOpen = opened);
    }

    public void OnCurrentVideoPathChanged()
    {
        CancelImageLoad();
        _thumbCache.Clear();
        _lastRequestedPositionMs = -1;
        _lastLoadedPositionMs = -1;
        ImageSource = null;
    }

    public HoverPopupController HoverPopupController => _hoverPopupController;

    public void OnMove(Point pos, double sliderWidth)
    {
        long length = _getMediaLength();
        if (length <= 0) return;

        double ratio = Math.Max(0, Math.Min(1, pos.X / sliderWidth));
        long hoverTimeMs = (long)(ratio * length);
        long hoverPositionMs = hoverTimeMs;

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

        if (hoverPositionMs == _lastRequestedPositionMs) return;
        _lastRequestedPositionMs = hoverPositionMs;

        if (thumbReady && currentVideoPath != null)
        {
            if (_thumbCache.TryGetValue(hoverPositionMs, out var cached))
            {
                _lastLoadedPositionMs = hoverPositionMs;
                ImageSource = cached;
            }
            else
            {
                _ = LoadJpegAsync(currentVideoPath, hoverPositionMs);
            }
        }
    }

    public void Close()
    {
        _hoverPopupController.CloseNow();
        CancelImageLoad();
    }

    private async Task LoadJpegAsync(string videoPath, long positionMs)
    {
        CancelImageLoad();
        _imageLoadCts = new CancellationTokenSource();
        var cancellationToken = _imageLoadCts.Token;
        int version = Interlocked.Increment(ref _imageLoadVersion);

        try
        {
            var bmp = await Task.Run(() => DecodeJpeg(videoPath, positionMs, cancellationToken), cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            if (version != _imageLoadVersion)
                return;

            if (_getCurrentVideoPath() != videoPath || _lastRequestedPositionMs != positionMs)
                return;

            if (bmp == null)
            {
                if (_lastLoadedPositionMs != positionMs)
                    ImageSource = null;
                return;
            }

            _thumbCache[positionMs] = bmp;
            _lastLoadedPositionMs = positionMs;
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

    private BitmapSource? DecodeJpeg(string videoPath, long positionMs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _playbackFacade.GetThumbnailPath(videoPath, positionMs);
        if (path == null)
        {
            Log.Debug(
                $"Thumbnail preview miss: file={System.IO.Path.GetFileName(videoPath)}, requestedMs={positionMs}, timeText={FormatTime(positionMs)}");
            return null;
        }

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




