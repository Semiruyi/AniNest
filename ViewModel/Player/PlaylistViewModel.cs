using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Localization;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel.Player;

public partial class PlaylistViewModel : ObservableObject
{
    private PlaylistManager _playlistManager = null!;
    private readonly ILocalizationService _loc;

    public PlaylistViewModel(ILocalizationService loc)
    {
        _loc = loc;
    }

    [ObservableProperty]
    private string _episodeCountText = "";

    [ObservableProperty]
    private string _currentFolderName = "";

    [ObservableProperty]
    private bool _isVisible = true;

    private int _currentIndex = -1;
    public int CurrentIndex
    {
        get => _currentIndex;
        set => SetCurrentIndex(value);
    }

    public PlaylistItem? CurrentItem => _playlistManager?.CurrentItem;

    public ObservableCollection<PlaylistItem> Items => _playlistManager?.Items
        ?? new ObservableCollection<PlaylistItem>();

    public void SetPlaylistManager(PlaylistManager playlistManager)
    {
        _playlistManager = playlistManager;
        _playlistManager.VideoPlayed += filePath =>
        {
            // CurrentVideoPath 由 PlayerViewModel 管理
        };
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        _playlistManager.LoadFolder(folderPath, folderName);
        CurrentFolderName = folderName;

        var items = _playlistManager.Items;
        EpisodeCountText = items.Count > 0 ? string.Format(_loc["Player.EpisodeCount"], items.Count) : "";
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public void ActivateCurrentVideo()
    {
        _playlistManager.PlayCurrentVideo();
    }

    public void SaveProgress()
    {
        _playlistManager.SaveProgress();
    }

    public bool PlayNext()
    {
        if (_playlistManager.PlayNext())
        {
            SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
            return true;
        }
        return false;
    }

    public bool PlayPrevious()
    {
        if (_playlistManager.PlayPrevious())
        {
            SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
            return true;
        }
        return false;
    }

    [RelayCommand]
    private void PlayEpisode(PlaylistItem item)
    {
        int index = item.Number - 1;
        if (index < 0 || index >= _playlistManager.Items.Count) return;
        if (index == CurrentIndex) return;

        if (CurrentIndex >= 0 && CurrentIndex < _playlistManager.Items.Count)
            _playlistManager.Items[CurrentIndex].IsPlayed = true;

        _playlistManager.PlayEpisode(index);
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    public void UpdateThumbnailReady(string videoPath)
    {
        _playlistManager.UpdateThumbnailReady(videoPath);
    }

    public void UpdateThumbnailProgress(string videoPath, int percent)
    {
        _playlistManager.UpdateThumbnailProgress(videoPath, percent);
    }

    public void RefreshCurrentIndex()
    {
        SetCurrentIndex(_playlistManager.CurrentIndex, force: true);
    }

    private void SetCurrentIndex(int value, bool force = false)
    {
        if (!force && _currentIndex == value)
            return;

        _currentIndex = value;
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(CurrentItem));
    }
}
