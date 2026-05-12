using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Features.Library.Models;

public partial class FolderListItem : ObservableObject
{
    public FolderListItem(string name, string path, int videoCount, string? coverPath)
    {
        Name = name;
        Path = path;
        VideoCount = videoCount;
        CoverPath = coverPath;
    }

    public string Name { get; }
    public string Path { get; }
    public int VideoCount { get; }
    public string? CoverPath { get; }

    [ObservableProperty]
    private double _playedPercent;

    [ObservableProperty]
    private int _playedCount;

    [ObservableProperty]
    private bool _isPopupOpen;

    [ObservableProperty]
    private bool _canMoveToFront;

    [ObservableProperty]
    private WatchStatus _status;

    [ObservableProperty]
    private bool _isFavorite;

    public int StatusMenuSelectedIndex => Status switch
    {
        WatchStatus.Watching => 0,
        WatchStatus.Unsorted => 1,
        WatchStatus.Completed => 2,
        WatchStatus.Dropped => 3,
        _ => -1,
    };

    public bool IsStatusWatching => Status == WatchStatus.Watching;
    public bool IsStatusUnsorted => Status == WatchStatus.Unsorted;
    public bool IsStatusCompleted => Status == WatchStatus.Completed;
    public bool IsStatusDropped => Status == WatchStatus.Dropped;

    partial void OnStatusChanged(WatchStatus value)
    {
        OnPropertyChanged(nameof(StatusMenuSelectedIndex));
        OnPropertyChanged(nameof(IsStatusWatching));
        OnPropertyChanged(nameof(IsStatusUnsorted));
        OnPropertyChanged(nameof(IsStatusCompleted));
        OnPropertyChanged(nameof(IsStatusDropped));
    }
}
