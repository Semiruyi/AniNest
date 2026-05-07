using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;
namespace LocalPlayer.Infrastructure.Media;

public class VideoFrameProvider : IDisposable
{
    private static readonly Logger Log = AppLog.For<VideoFrameProvider>();
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private readonly byte[] _blankFrame;
    private WriteableBitmap? _bitmap;
    private readonly byte[][] _buffers;
    private int _bufferIndex;
    private GCHandle _bufferHandle;
    private byte[]? _readyBuffer;
    private readonly object _lock = new();
    private string? _observationFileName;
    private bool _firstLockObserved;
    private bool _firstUnlockObserved;
    private bool _firstDisplayObserved;
    private bool _firstPresentedObserved;

    public WriteableBitmap? Bitmap => _bitmap;
    public event EventHandler? FramePresented;
    public event EventHandler? FirstFrameLocked;
    public event EventHandler? FirstFrameUnlocked;
    public event EventHandler? FirstFrameDisplayQueued;

    public VideoFrameProvider(int width = 1920, int height = 1080)
    {
        _width = width;
        _height = height;
        _stride = width * 4;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _blankFrame = new byte[width * height * 4];
        _buffers = new byte[2][];
        for (int i = 0; i < 2; i++)
            _buffers[i] = new byte[width * height * 4];
    }

    public void AttachToPlayer(LibVLCSharp.Shared.MediaPlayer player)
    {
        player.SetVideoFormat("BGRA", (uint)_width, (uint)_height, (uint)_stride);
        player.SetVideoCallbacks(VideoLock, VideoUnlock, VideoDisplay);
        Log.Info($"AttachToPlayer: {_width}x{_height}, stride={_stride}");
    }

    public void BeginFrameObservation(string? filePath)
    {
        lock (_lock)
        {
            _observationFileName = string.IsNullOrWhiteSpace(filePath)
                ? null
                : System.IO.Path.GetFileName(filePath);
            _firstLockObserved = false;
            _firstUnlockObserved = false;
            _firstDisplayObserved = false;
            _firstPresentedObserved = false;
        }
    }

    public void ClearBitmap()
    {
        Log.Info(MemorySnapshot.Capture("VideoFrameProvider.ClearBitmap.begin",
            ("bitmap", _bitmap != null),
            ("readyBuffer", _readyBuffer != null)));

        var application = Application.Current;
        if (application?.Dispatcher == null)
        {
            ClearBitmapCore();
            Log.Info(MemorySnapshot.Capture("VideoFrameProvider.ClearBitmap.end",
                ("bitmap", _bitmap != null),
                ("readyBuffer", _readyBuffer != null)));
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            ClearBitmapCore();
            Log.Info(MemorySnapshot.Capture("VideoFrameProvider.ClearBitmap.end",
                ("bitmap", _bitmap != null),
                ("readyBuffer", _readyBuffer != null)));
            return;
        }

        application.Dispatcher.Invoke(ClearBitmapCore);
        Log.Info(MemorySnapshot.Capture("VideoFrameProvider.ClearBitmap.end",
            ("bitmap", _bitmap != null),
            ("readyBuffer", _readyBuffer != null)));
    }

    private IntPtr VideoLock(IntPtr opaque, IntPtr planes)
    {
        bool isFirstLock = false;
        IntPtr ptr;
        lock (_lock)
        {
            _bufferIndex = (_bufferIndex + 1) % 2;
            var buf = _buffers[_bufferIndex];
            _bufferHandle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            ptr = _bufferHandle.AddrOfPinnedObject();
            Marshal.WriteIntPtr(planes, ptr);
            Log.Debug($"VideoLock: bufferIndex={_bufferIndex}");
            if (!_firstLockObserved)
            {
                _firstLockObserved = true;
                isFirstLock = true;
            }
        }

        if (isFirstLock)
        {
            using var span = PerfSpan.Begin("VideoFrameProvider.FirstFrameLock", CreateObservationTags());
            FirstFrameLocked?.Invoke(this, EventArgs.Empty);
        }

        return ptr;
    }

    private void VideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        bool isFirstUnlock = false;
        lock (_lock)
        {
            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();
            _readyBuffer = _buffers[_bufferIndex];
            Log.Debug($"VideoUnlock: bufferIndex={_bufferIndex}");
            if (!_firstUnlockObserved)
            {
                _firstUnlockObserved = true;
                isFirstUnlock = true;
            }
        }

        if (isFirstUnlock)
        {
            using var span = PerfSpan.Begin("VideoFrameProvider.FirstFrameUnlock", CreateObservationTags());
            FirstFrameUnlocked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void VideoDisplay(IntPtr opaque, IntPtr picture)
    {
        byte[]? readyBuf;
        bool isFirstDisplay;
        lock (_lock)
        {
            readyBuf = _readyBuffer;
            _readyBuffer = null;
            isFirstDisplay = !_firstDisplayObserved;
            if (isFirstDisplay)
                _firstDisplayObserved = true;
        }

        if (readyBuf == null) return;
        Log.Debug("VideoDisplay: frame ready");

        if (isFirstDisplay)
        {
            using var span = PerfSpan.Begin("VideoFrameProvider.FirstFrameDisplayQueued", CreateObservationTags());
            FirstFrameDisplayQueued?.Invoke(this, EventArgs.Empty);
        }

        var w = _width;
        var h = _height;
        var stride = _stride;
        var dispatchSpan = isFirstDisplay
            ? PerfSpan.Begin("VideoFrameProvider.FirstFrameDispatchToPresent", CreateObservationTags())
            : null;

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var wb = _bitmap;
                if (wb == null)
                    return;
                wb.WritePixels(new Int32Rect(0, 0, w, h), readyBuf, stride, 0);
                if (isFirstDisplay && !_firstPresentedObserved)
                {
                    _firstPresentedObserved = true;
                }
                FramePresented?.Invoke(this, EventArgs.Empty);
            }
            catch { }
            finally
            {
                dispatchSpan?.Dispose();
            }
        }, DispatcherPriority.Render);
    }

    private void ClearBitmapCore()
    {
        lock (_lock)
        {
            _readyBuffer = null;
        }

        var wb = _bitmap;
        if (wb == null)
            return;

        wb.WritePixels(new Int32Rect(0, 0, _width, _height), _blankFrame, _stride, 0);
    }

    private IReadOnlyDictionary<string, string>? CreateObservationTags()
    {
        if (string.IsNullOrWhiteSpace(_observationFileName))
            return null;

        return new Dictionary<string, string>
        {
            ["file"] = _observationFileName
        };
    }

    public void Dispose()
    {
        Log.Info(MemorySnapshot.Capture("VideoFrameProvider.Dispose.begin",
            ("bitmap", _bitmap != null),
            ("readyBuffer", _readyBuffer != null)));
        lock (_lock)
        {
            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();
            _readyBuffer = null;
        }
        _bitmap = null;
        Log.Info(MemorySnapshot.Capture("VideoFrameProvider.Dispose.end",
            ("bitmap", _bitmap != null),
            ("readyBuffer", _readyBuffer != null)));
    }
}



