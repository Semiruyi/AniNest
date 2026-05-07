using System.ComponentModel;

namespace AniNest.Features.Player.Models;

public class PlaylistItem : INotifyPropertyChanged
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string FilePath { get; set; } = "";

    private bool _isPlayed;
    public bool IsPlayed
    {
        get => _isPlayed;
        set
        {
            if (_isPlayed != value)
            {
                _isPlayed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlayed)));
            }
        }
    }

    private bool _isThumbnailReady;
    public bool IsThumbnailReady
    {
        get => _isThumbnailReady;
        set
        {
            if (_isThumbnailReady != value)
            {
                _isThumbnailReady = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsThumbnailReady)));
            }
        }
    }

    private int _thumbnailProgress;
    public int ThumbnailProgress
    {
        get => _thumbnailProgress;
        set
        {
            if (_thumbnailProgress != value)
            {
                _thumbnailProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailProgress)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
