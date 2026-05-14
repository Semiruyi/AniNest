using System.ComponentModel;
using System.Windows.Media;

namespace AniNest.Infrastructure.Media;

public sealed class WpfVideoSurfaceSource : IWpfVideoSurfaceSource
{
    private readonly MediaPlayerController _mediaPlayerController;

    public WpfVideoSurfaceSource(MediaPlayerController mediaPlayerController)
    {
        _mediaPlayerController = mediaPlayerController;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageSource? CurrentFrame => _mediaPlayerController.VideoBitmap;

    public void Refresh()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFrame)));
}
