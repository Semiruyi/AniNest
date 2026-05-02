using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Messages;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel.Player;

public partial class PlayerViewModel : ObservableObject
{
    private static readonly Logger Log = AppLog.For<PlayerViewModel>();
    private readonly ISettingsService _settings;
    private readonly IMediaPlayerController _media;
    private readonly PlayerInputHandler _inputHandler;

    private PlaylistManager _playlistManager = null!;
    private DispatcherTimer _saveTimer = null!;
    private bool _initialized;
    private float _savedRate = 1.0f;
    private bool _savedPlaylistVisible;

    // ========== 子 ViewModel ==========

    public ControlBarViewModel ControlBar { get; }
    public PlaylistViewModel Playlist { get; }

    // ========== 视频区绑定 ==========

    public ImageSource? VideoSource => _media.VideoBitmap;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string? _currentVideoPath;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private int _currentIndex = -1;

    public PlaylistItem? CurrentItem => _playlistManager.CurrentItem;

    public System.Collections.ObjectModel.ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    // ========== 格式化 ==========

    public static string FormatTime(long ms) => MediaPlayerController.FormatTime(ms);

    public Dictionary<string, Key> GetCurrentBindings() => _inputHandler.GetCurrentBindings();
    public void ReloadBindings() => _inputHandler.ReloadBindings();
    public bool HandleKeyDown(KeyEventArgs e) => _inputHandler.HandleKeyDown(e);

    // ========== 构造 ==========

    public PlayerViewModel(ISettingsService settings, IThumbnailGenerator thumbnailGenerator,
                           IMediaPlayerController media)
    {
        _settings = settings;
        _media = media;
        _initialized = true;

        // 子 ViewModel
        _inputHandler = new PlayerInputHandler(_settings);
        _playlistManager = new PlaylistManager(_settings, _media,
            path => thumbnailGenerator.GetState(path));

        ControlBar = new ControlBarViewModel(_media, _inputHandler, thumbnailGenerator);
        Playlist = new PlaylistViewModel();
        Playlist.SetPlaylistManager(_playlistManager);

        // 跨组件连线
        ControlBar.NextRequested += () => PlayNext();
        ControlBar.PreviousRequested += () => PlayPrevious();
        ControlBar.GoBackRequested += () => GoBackInternal();
        ControlBar.TogglePlaylistRequested += () => Playlist.IsVisible = !Playlist.IsVisible;

        // 全屏消息
        WeakReferenceMessenger.Default.Register<ToggleFullscreenMessage>(this, (_, _) =>
        {
            IsFullscreen = !IsFullscreen;
            ControlBar.IsFullscreen = IsFullscreen;
            if (IsFullscreen)
            {
                _savedPlaylistVisible = Playlist.IsVisible;
                Playlist.IsVisible = false;
            }
            else
            {
                Playlist.IsVisible = _savedPlaylistVisible;
            }
        });

        // 播放列表事件
        _playlistManager.VideoPlayed += filePath =>
        {
            CurrentVideoPath = filePath;
            ControlBar.CurrentVideoPath = filePath;
            ControlBar.ThumbnailPreview.OnCurrentVideoPathChanged();
        };

        // 缩略图事件
        thumbnailGenerator.VideoReady += path =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_initialized)
                {
                    Log.Debug($"VideoReady → UpdateThumbnailReady: {Path.GetFileName(path)}");
                    _playlistManager.UpdateThumbnailReady(path);
                }
                else
                    Log.Debug($"VideoReady 跳过 (_initialized=false): {Path.GetFileName(path)}");
            });
        thumbnailGenerator.VideoProgress += (path, percent) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_initialized)
                {
                    Log.Debug($"VideoProgress → UpdateThumbnailProgress: {Path.GetFileName(path)}={percent}%");
                    _playlistManager.UpdateThumbnailProgress(path, percent);
                }
                else
                    Log.Debug($"VideoProgress 跳过 (_initialized=false): {Path.GetFileName(path)}={percent}%");
            });

        // 定时保存
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _saveTimer.Tick += (_, _) => SaveProgress();

        WireMediaEvents();
        WireInputEvents();
        _inputHandler.ReloadBindings();

        WeakReferenceMessenger.Default.Register<LoadPlayerFolderMessage>(this, (_, m) =>
            LoadFolder(m.Path, m.Name));

        _media.Initialize();
    }

    // ========== 媒体事件 → 状态同步 ==========

    private void WireMediaEvents()
    {
        _media.Playing += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = true;
                ControlBar.UpdateIsPlaying(true);
                _saveTimer.Start();
            });

        _media.Paused += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                ControlBar.UpdateIsPlaying(false);
                _saveTimer.Stop();
                SaveProgress();
            });

        _media.Stopped += (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPlaying = false;
                ControlBar.UpdateIsPlaying(false);
                _saveTimer.Stop();
            });

        _media.ProgressUpdated += (_, args) =>
        {
            if (!ControlBar.IsSeeking)
                ControlBar.UpdateProgress(args.CurrentTime, args.TotalTime);
        };
    }

    private void WireInputEvents()
    {
        _inputHandler.TogglePlayPause += (_, _) => _media.TogglePlayPause();
        _inputHandler.SeekForward += (_, _) => _media.SeekForward(5000);
        _inputHandler.SeekBackward += (_, _) => _media.SeekBackward(5000);
        _inputHandler.NextEpisode += (_, _) => PlayNext();
        _inputHandler.PreviousEpisode += (_, _) => PlayPrevious();
        _inputHandler.Back += (_, _) => GoBackInternal();
    }

    // ========== 文件夹加载 ==========

    public void LoadFolder(string folderPath, string folderName)
    {
        Playlist.LoadFolder(folderPath, folderName);
        CurrentIndex = Playlist.CurrentIndex;
    }

    // ========== 选集导航 ==========

    private void PlayNext()
    {
        if (Playlist.PlayNext())
            CurrentIndex = Playlist.CurrentIndex;
    }

    private void PlayPrevious()
    {
        if (Playlist.PlayPrevious())
            CurrentIndex = Playlist.CurrentIndex;
    }

    public void SaveProgress()
    {
        Playlist.SaveProgress();
    }

    // ========== 视频区手势命令 ==========

    [RelayCommand]
    private void PlayPause() => _media.TogglePlayPause();

    [RelayCommand]
    private void GoBack() => GoBackInternal();

    [RelayCommand]
    private void EnterRightHold()
    {
        _savedRate = ControlBar.Rate;
        ControlBar.Rate = 3.0f;
        _media.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        ControlBar.Rate = _savedRate;
        _media.Rate = _savedRate;
    }

    private void GoBackInternal()
    {
        SaveProgress();
        WeakReferenceMessenger.Default.Send(new BackRequestedMessage());
    }

    // ========== 键盘 ==========

    [RelayCommand]
    private void KeyDown(KeyEventArgs e) => HandleKeyDown(e);

    // ========== 生命周期 ==========

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

    public void SetRate(float rate) { _media.Rate = rate; ControlBar.Rate = rate; }

    public long MediaLength => _media.Length;
}
