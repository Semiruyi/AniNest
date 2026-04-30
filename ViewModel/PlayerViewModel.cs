using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Media;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel;

public partial class PlayerViewModel : ObservableObject
{
    private static void Log(string message) => AppLog.Info(nameof(PlayerViewModel), message);

    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    private IMediaPlayerController _media = null!;
    private PlaylistManager _playlistManager = null!;
    private PlayerInputHandler _inputHandler = null!;
    private DispatcherTimer _saveTimer = null!;
    private bool _initialized;

    private bool _updatingSelection;
    private float _savedRate = 1.0f;

    // ========== Playlist ==========

    [ObservableProperty]
    private string _episodeCountText = "";

    [ObservableProperty]
    private string _currentFolderName = "";

    private int _currentIndex = -1;
    public int CurrentIndex
    {
        get => _currentIndex;
        private set
        {
            if (_currentIndex != value)
            {
                _currentIndex = value;
                OnPropertyChanged();
            }
        }
    }

    // ========== Playback State ==========

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private long _currentTime;

    [ObservableProperty]
    private long _totalTime;

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalTimeText = "00:00";

    [ObservableProperty]
    private float _rate = 1.0f;

    // ========== UI State ==========

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _isPlaylistVisible = true;

    [ObservableProperty]
    private bool _isSeeking;

    /// <summary>当前播放视频路径，用于 ControlBar 的缩略图预览定位</summary>
    [ObservableProperty]
    private string? _currentVideoPath;

    // ========== Events ==========

    public event Action? BackRequested;
    public event Action? FullscreenToggled;

    // ========== Constructor ==========

    public PlayerViewModel(ISettingsService settings, IThumbnailGenerator thumbnailGenerator)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;

        _thumbnailGenerator.VideoReady += path =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_initialized)
                    _playlistManager.UpdateThumbnailReady(path);
            });
        _thumbnailGenerator.VideoProgress += (path, percent) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_initialized)
                    _playlistManager.UpdateThumbnailProgress(path, percent);
            });
    }

    /// <summary>
    /// 必须在构造函数之后、使用之前调用，传入与 PlayerPage 共享的 IMediaPlayerController 实例。
    /// </summary>
    public void Initialize(IMediaPlayerController media)
    {
        if (_initialized) return;
        _initialized = true;
        _media = media;

        _playlistManager = new PlaylistManager(_settings, media,
            path => _thumbnailGenerator.GetState(path));

        _playlistManager.VideoPlayed += filePath =>
        {
            CurrentVideoPath = filePath;
        };

        _inputHandler = new PlayerInputHandler(_settings);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _saveTimer.Tick += (_, _) => SaveProgress();

        WireMediaEvents();
        WireInputEvents();

        _inputHandler.ReloadBindings();
    }

    private void WireMediaEvents()
    {
        _media.Playing += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = true;
                _saveTimer.Start();
            });

        _media.Paused += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                _saveTimer.Stop();
                SaveProgress();
            });

        _media.Stopped += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                _saveTimer.Stop();
            });

        _media.ProgressUpdated += (_, args) =>
        {
            if (!IsSeeking)
            {
                TotalTime = args.TotalTime;
                CurrentTime = args.CurrentTime;
                CurrentTimeText = MediaPlayerController.FormatTime(args.CurrentTime);
                TotalTimeText = MediaPlayerController.FormatTime(args.TotalTime);
            }
        };
    }

    private void WireInputEvents()
    {
        _inputHandler.TogglePlayPause += (_, _) => _media.TogglePlayPause();
        _inputHandler.SeekForward += (_, _) => _media.SeekForward(5000);
        _inputHandler.SeekBackward += (_, _) => _media.SeekBackward(5000);
        _inputHandler.NextEpisode += (_, _) => PlayNextInternal();
        _inputHandler.PreviousEpisode += (_, _) => PlayPreviousInternal();
        _inputHandler.ToggleFullscreen += (_, _) => FullscreenToggled?.Invoke();
        _inputHandler.ExitFullscreen += (_, _) => FullscreenToggled?.Invoke();
        _inputHandler.Back += (_, _) =>
        {
            SaveProgress();
            BackRequested?.Invoke();
        };
    }

    // ========== Folder Load ==========

    public void LoadFolder(string folderPath, string folderName)
    {
        _playlistManager.LoadFolder(folderPath, folderName);
        CurrentFolderName = folderName;

        var items = _playlistManager.Items;
        EpisodeCountText = items.Count > 0 ? $"{items.Count} 集" : "";

        CurrentIndex = _playlistManager.CurrentIndex;
    }

    public PlaylistItem? CurrentItem => _playlistManager.CurrentItem;

    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    // ========== Selection ==========

    public void SelectEpisode(int index)
    {
        if (_updatingSelection)
        {
            _updatingSelection = false;
            return;
        }
        _playlistManager.PlayEpisode(index);
        CurrentIndex = _playlistManager.CurrentIndex;
    }

    private void PlayNextInternal()
    {
        if (_playlistManager.PlayNext())
        {
            _updatingSelection = true;
            CurrentIndex = _playlistManager.CurrentIndex;
        }
    }

    private void PlayPreviousInternal()
    {
        if (_playlistManager.PlayPrevious())
        {
            _updatingSelection = true;
            CurrentIndex = _playlistManager.CurrentIndex;
        }
    }

    // ========== Progress ==========

    public void SaveProgress()
    {
        _playlistManager.SaveProgress();
    }

    // ========== Input ==========

    public bool HandleKeyDown(KeyEventArgs e, bool isFullscreen)
        => _inputHandler.HandleKeyDown(e, isFullscreen);

    public Dictionary<string, Key> GetCurrentBindings()
        => _inputHandler.GetCurrentBindings();

    public void ReloadBindings()
        => _inputHandler.ReloadBindings();

    // ========== Commands ==========

    [RelayCommand]
    private void PlayPause() => _media.TogglePlayPause();

    [RelayCommand]
    private void Stop() => _media.Stop();

    [RelayCommand]
    private void Next()
    {
        if (_playlistManager.PlayNext())
        {
            _updatingSelection = true;
            CurrentIndex = _playlistManager.CurrentIndex;
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (_playlistManager.PlayPrevious())
        {
            _updatingSelection = true;
            CurrentIndex = _playlistManager.CurrentIndex;
        }
    }

    [RelayCommand]
    private void ToggleFullscreen() => FullscreenToggled?.Invoke();

    [RelayCommand]
    private void TogglePlaylist() => IsPlaylistVisible = !IsPlaylistVisible;

    [RelayCommand]
    private void Seek(long time) => _media.SeekTo(time);

    [RelayCommand]
    private void ChangeSpeed(float speed)
    {
        _media.Rate = speed;
        Rate = speed;
    }

    [RelayCommand]
    private void GoBack()
    {
        SaveProgress();
        BackRequested?.Invoke();
    }

    // ========== Input handler exposed for ControlBar ==========

    public PlayerInputHandler InputHandler => _inputHandler;

    // ========== Settings ==========

    public void OpenKeyBindingsSettings()
    {
        var window = new View.Settings.KeyBindingsWindow(_inputHandler)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    // ========== Right-hold speed ==========

    public void EnterHoldSpeed(float holdSpeed = 3.0f)
    {
        _savedRate = Rate;
        Rate = holdSpeed;
        _media.Rate = holdSpeed;
    }

    public void ExitHoldSpeed()
    {
        Rate = _savedRate;
        _media.Rate = _savedRate;
    }

    // ========== Thumbnail ==========

    public string? GetThumbnailPath(string videoPath, int second)
        => _thumbnailGenerator.GetThumbnailPath(videoPath, second);

    public long MediaLength => _media.Length;
}
