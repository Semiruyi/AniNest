using System.ComponentModel;

namespace LocalPlayer.Models;

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
