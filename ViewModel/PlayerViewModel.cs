using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Converters;
using LocalPlayer.Messages;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel;

public partial class PlayerViewModel : ObservableObject
{
    public static void Log(string message) => AppLog.Info(nameof(PlayerViewModel), message);
    public static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(PlayerViewModel), message, ex);

    private readonly ISettingsService _settings;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    private IMediaPlayerController _media = null!;
    private PlaylistManager _playlistManager = null!;
    private PlayerInputHandler _inputHandler = null!;
    private DispatcherTimer _saveTimer = null!;
    private bool _initialized;

    private float _savedRate = 1.0f;


    // — 媒体源（替代 View 直接访问 _media.VideoBitmap）—
    public System.Windows.Media.ImageSource? VideoSource => _media.VideoBitmap;

    // — 格式化时间（替代 MediaPlayerController.FormatTime 静态调用）—
    public static string FormatTime(long ms) => MediaPlayerController.FormatTime(ms);

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

    public static float[] SpeedOptions { get; } = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f };

    // ========== 倍速弹窗开关 ==========

    [ObservableProperty]
    private bool _isSpeedPopupOpen;
    private DispatcherTimer? _speedCloseTimer;

    public void OnSpeedEnter()
    {
        _speedCloseTimer?.Stop();
        IsSpeedPopupOpen = true;
    }

    [RelayCommand]
    private void SpeedEnter() => OnSpeedEnter();

    [RelayCommand]
    private void SpeedLeave() => OnSpeedLeave();

    public void OnSpeedLeave()
    {
        if (_speedCloseTimer == null)
        {
            _speedCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _speedCloseTimer.Tick += (_, _) =>
            {
                _speedCloseTimer.Stop();
                IsSpeedPopupOpen = false;
            };
        }
        else
        {
            _speedCloseTimer.Stop();
        }
        _speedCloseTimer.Start();
    }

    public void CloseSpeedPopup()
    {
        _speedCloseTimer?.Stop();
        IsSpeedPopupOpen = false;
    }

    // ========== 缩略图预览弹窗 ==========

    [ObservableProperty]
    private bool _isThumbnailOpen;
    [ObservableProperty]
    private ImageSource? _thumbnailImageSource;
    [ObservableProperty]
    private string _thumbnailTimeText = "";
    [ObservableProperty]
    private Visibility _thumbnailImageVisibility = Visibility.Collapsed;
    [ObservableProperty]
    private double _thumbnailHOffset;

    private readonly Dictionary<int, BitmapSource> _thumbCache = new();
    private DispatcherTimer? _thumbShowTimer;
    private DispatcherTimer? _thumbHideTimer;
    private bool _thumbHovering;
    private bool _thumbVisible;
    private bool _thumbClosing;
    private int _lastRequestedSecond = -1;

    partial void OnCurrentVideoPathChanged(string? value)
    {
        _thumbCache.Clear();
        _lastRequestedSecond = -1;
    }

    public void OnThumbnailEnter()
    {
        _thumbHovering = true;
        _thumbHideTimer?.Stop();
        if (!_thumbVisible)
            (_thumbShowTimer ??= CreateThumbShowTimer()).Start();
    }

    [RelayCommand]
    private void ThumbnailEnter() => OnThumbnailEnter();

    [RelayCommand]
    private void ThumbnailLeave() => OnThumbnailLeave();

    public void OnThumbnailLeave()
    {
        _thumbHovering = false;
        _thumbShowTimer?.Stop();
        (_thumbHideTimer ??= CreateThumbHideTimer()).Start();
    }

    public void OnThumbnailPopupEnter()
    {
        _thumbHideTimer?.Stop();
    }

    [RelayCommand]
    private void ThumbnailPopupEnter() => OnThumbnailPopupEnter();

    [RelayCommand]
    private void ThumbnailPopupLeave() => OnThumbnailPopupLeave();

    public void OnThumbnailPopupLeave()
    {
        _thumbHideTimer?.Stop();
        (_thumbHideTimer ??= CreateThumbHideTimer()).Start();
    }

    public void OnThumbnailMove(Point pos, double sliderWidth)
    {
        long length = MediaLength;
        if (length <= 0) return;

        double ratio = Math.Max(0, Math.Min(1, pos.X / sliderWidth));
        long hoverTimeMs = (long)(ratio * length);
        int hoverSecond = (int)(hoverTimeMs / 1000);

        ThumbnailTimeText = FormatTime(hoverTimeMs);
        double popupW = 160;
        ThumbnailHOffset = Math.Max(0, Math.Min(pos.X - popupW / 2, sliderWidth - popupW));

        bool thumbReady = CurrentVideoPath != null &&
            GetThumbnailState(CurrentVideoPath) == ThumbnailState.Ready;
        ThumbnailImageVisibility = thumbReady ? Visibility.Visible : Visibility.Collapsed;

        if (hoverSecond == _lastRequestedSecond) return;
        _lastRequestedSecond = hoverSecond;

        if (thumbReady && CurrentVideoPath != null)
        {
            if (_thumbCache.TryGetValue(hoverSecond, out var cached))
            {
                ThumbnailImageSource = cached;
            }
            else
            {
                var bmp = LoadThumbnailJpeg(CurrentVideoPath, hoverSecond);
                if (bmp != null)
                {
                    _thumbCache[hoverSecond] = bmp;
                    ThumbnailImageSource = bmp;

                    if (_thumbCache.Count > 20)
                    {
                        var toRemove = _thumbCache.Keys.OrderBy(k => k).Take(_thumbCache.Count / 2).ToList();
                        foreach (var k in toRemove) _thumbCache.Remove(k);
                    }
                }
            }
        }
    }

    [RelayCommand]
    private void ThumbnailMove(MouseEventArgs e)
    {
        if (e.Source is FrameworkElement el)
            OnThumbnailMove(e.GetPosition(el), el.ActualWidth);
    }

    private void ShowThumbnail()
    {
        if (_thumbVisible || _thumbClosing) return;
        _thumbVisible = true;
        IsThumbnailOpen = true;
    }

    private void HideThumbnail()
    {
        if (!_thumbVisible || _thumbClosing) return;
        _thumbClosing = true;

        // Reset state immediately — animation completion is handled by PopupAnimator.BindOpen
        _thumbVisible = false;
        _thumbClosing = false;
        IsThumbnailOpen = false;
    }

    private DispatcherTimer CreateThumbShowTimer()
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

    private DispatcherTimer CreateThumbHideTimer()
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

    private BitmapSource? LoadThumbnailJpeg(string videoPath, int second)
    {
        var path = GetThumbnailPath(videoPath, second);
        if (path == null) return null;
        try
        {
            var decoder = new JpegBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            LogError($"缩略图解码异常 second={second}", ex);
            return null;
        }
    }

    // ========== UI State ==========

    [ObservableProperty]
    private bool _isPlaylistVisible = true;

    [ObservableProperty]
    private bool _isSeeking;

    /// <summary>当前播放视频路径，用于 ControlBar 的缩略图预览定位</summary>
    [ObservableProperty]
    private string? _currentVideoPath;

    // ========== Constructor ==========

    public PlayerViewModel(ISettingsService settings, IThumbnailGenerator thumbnailGenerator,
                           IMediaPlayerController media)
    {
        _settings = settings;
        _thumbnailGenerator = thumbnailGenerator;
        _media = media;
        _initialized = true;

        _playlistManager = new PlaylistManager(_settings, media,
            path => _thumbnailGenerator.GetState(path));

        _playlistManager.VideoPlayed += filePath =>
        {
            CurrentVideoPath = filePath;
        };

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

        _inputHandler = new PlayerInputHandler(_settings);
        _inputHandler.BindingsChanged += () =>
        {
            OnPropertyChanged(nameof(PlayPauseTooltip));
            OnPropertyChanged(nameof(PreviousTooltip));
            OnPropertyChanged(nameof(NextTooltip));
        };

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _saveTimer.Tick += (_, _) => SaveProgress();

        WireMediaEvents();
        WireInputEvents();

        _inputHandler.ReloadBindings();

        WeakReferenceMessenger.Default.Register<LoadPlayerFolderMessage>(this, (_, m) =>
            LoadFolder(m.Path, m.Name));

        _media.Initialize();
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
        _inputHandler.Back += (_, _) =>
        {
            SaveProgress();
            WeakReferenceMessenger.Default.Send(new BackRequestedMessage());
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

        _playlistManager.PlayCurrentVideo();
    }

    public PlaylistItem? CurrentItem => _playlistManager.CurrentItem;

    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    private void PlayNextInternal()
    {
        if (_playlistManager.PlayNext())
            CurrentIndex = _playlistManager.CurrentIndex;
    }

    private void PlayPreviousInternal()
    {
        if (_playlistManager.PlayPrevious())
            CurrentIndex = _playlistManager.CurrentIndex;
    }

    // ========== Progress ==========

    public void SaveProgress()
    {
        _playlistManager.SaveProgress();
    }

    // ========== Input ==========

    public bool HandleKeyDown(KeyEventArgs e)
        => _inputHandler.HandleKeyDown(e);

    [RelayCommand]
    private void KeyDown(KeyEventArgs e) => HandleKeyDown(e);

    public Dictionary<string, Key> GetCurrentBindings()
        => _inputHandler.GetCurrentBindings();

    public void ReloadBindings()
        => _inputHandler.ReloadBindings();

    public string PlayPauseTooltip => FormatTooltip("播放/暂停", "TogglePlayPause");
    public string PreviousTooltip => FormatTooltip("上一集", "PreviousEpisode");
    public string NextTooltip => FormatTooltip("下一集", "NextEpisode");

    private string FormatTooltip(string label, string actionName)
    {
        var bindings = _inputHandler.GetCurrentBindings();
        var key = bindings.TryGetValue(actionName, out var k) ? k : Key.None;
        return key == Key.None ? label : $"{label} ({KeyDisplayConverter.Format(key)})";
    }

    // ========== Commands ==========

    [RelayCommand]
    private void PlayPause() => _media.TogglePlayPause();

    [RelayCommand]
    private void Stop() => _media.Stop();

    [RelayCommand]
    private void Next()
    {
        if (_playlistManager.PlayNext())
            CurrentIndex = _playlistManager.CurrentIndex;
    }

    [RelayCommand]
    private void Previous()
    {
        if (_playlistManager.PlayPrevious())
            CurrentIndex = _playlistManager.CurrentIndex;
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
        CurrentIndex = _playlistManager.CurrentIndex;
    }

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
        WeakReferenceMessenger.Default.Send(new BackRequestedMessage());
    }

    // ========== Right-hold commands — 供 XAML 手势绑定 ==========

    [RelayCommand]
    private void EnterRightHold()
    {
        _savedRate = Rate;
        Rate = 3.0f;
        _media.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        Rate = _savedRate;
        _media.Rate = _savedRate;
    }

    // ========== Input handler exposed for ControlBar ==========

    public PlayerInputHandler InputHandler => _inputHandler;

    // ========== Media lifecycle ==========

    [RelayCommand]
    private void Initialize() => _media.Initialize();

    [RelayCommand]
    private void Cleanup()
    {
        _initialized = false;
        _saveTimer.Stop();
        SaveProgress();
        _media.Dispose();
    }

    public void SetRate(float rate) { _media.Rate = rate; Rate = rate; }

    // ========== Thumbnail helpers ==========

    public ThumbnailState GetThumbnailState(string path) => _thumbnailGenerator.GetState(path);

    public string? GetThumbnailPath(string videoPath, int second)
        => _thumbnailGenerator.GetThumbnailPath(videoPath, second);

    public long MediaLength => _media.Length;

}
