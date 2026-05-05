using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace LocalPlayer.Infrastructure.Model;

public class VideoFrameProvider : IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private WriteableBitmap? _bitmap;
    private readonly byte[][] _buffers;
    private int _bufferIndex;
    private GCHandle _bufferHandle;
    private byte[]? _readyBuffer;
    private readonly object _lock = new();

    public WriteableBitmap? Bitmap => _bitmap;

    public VideoFrameProvider(int width = 1920, int height = 1080)
    {
        _width = width;
        _height = height;
        _stride = width * 4;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _buffers = new byte[2][];
        for (int i = 0; i < 2; i++)
            _buffers[i] = new byte[width * height * 4];
    }

    public void AttachToPlayer(LibVLCSharp.Shared.MediaPlayer player)
    {
        // BGRA 涓?WPF PixelFormats.Bgra32 鐨勫瓧鑺傚簭瀹屽叏涓€鑷达紝鏃犻渶 R/B 浜ゆ崲
        player.SetVideoFormat("BGRA", (uint)_width, (uint)_height, (uint)_stride);
        player.SetVideoCallbacks(VideoLock, VideoUnlock, VideoDisplay);
    }

    private IntPtr VideoLock(IntPtr opaque, IntPtr planes)
    {
        lock (_lock)
        {
            _bufferIndex = (_bufferIndex + 1) % 2;
            var buf = _buffers[_bufferIndex];
            _bufferHandle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            IntPtr ptr = _bufferHandle.AddrOfPinnedObject();
            Marshal.WriteIntPtr(planes, ptr);
            return ptr;
        }
    }

    private void VideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        lock (_lock)
        {
            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();
            _readyBuffer = _buffers[_bufferIndex];
        }
    }

    private void VideoDisplay(IntPtr opaque, IntPtr picture)
    {
        byte[]? readyBuf;
        lock (_lock)
        {
            readyBuf = _readyBuffer;
            _readyBuffer = null;
        }

        if (readyBuf == null || _bitmap == null) return;

        var wb = _bitmap;
        var w = _width;
        var h = _height;
        var stride = _stride;

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                wb.WritePixels(new Int32Rect(0, 0, w, h), readyBuf, stride, 0);
            }
            catch { }
        }, DispatcherPriority.Render);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();
            _readyBuffer = null;
        }
        _bitmap = null;
    }
}

