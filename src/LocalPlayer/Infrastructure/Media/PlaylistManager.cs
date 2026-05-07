using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.Features.Player.Models;
namespace LocalPlayer.Infrastructure.Media;

public class PlaylistManager
{
    private static readonly Logger Log = AppLog.For<PlaylistManager>();

    private readonly ISettingsService _settings;
    private readonly IMediaPlayerController _media;
    private readonly IVideoScanner _videoScanner;
    private readonly Func<string, ThumbnailState> _getThumbnailState;
    private Task<(bool[] Played, bool[] Thumb)>? _preloadTask;

    public string CurrentFolderPath { get; private set; } = "";
    public string CurrentFolderName { get; private set; } = "";
    public string[] VideoFiles { get; private set; } = Array.Empty<string>();
    public ObservableCollection<PlaylistItem> Items { get; private set; } = new();
    public int CurrentIndex { get; set; } = -1;

    public PlaylistItem? CurrentItem =>
        CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex] : null;

    public int ItemCount => Items.Count;

    public event Action<string>? VideoPlayed;

    public PlaylistManager(ISettingsService settings, IMediaPlayerController media,
                           IVideoScanner videoScanner,
                           Func<string, ThumbnailState> getThumbnailState)
    {
        _settings = settings;
        _media = media;
        _videoScanner = videoScanner;
        _getThumbnailState = getThumbnailState;
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        LoadFolderSkeleton(folderPath, folderName);
        LoadFolderData();
    }

    public void LoadFolderSkeleton(string folderPath, string folderName)
    {
        using var loadSpan = PerfSpan.Begin("Playlist.LoadFolderSkeleton", new Dictionary<string, string>
        {
            ["folder"] = folderName
        });

        CurrentFolderPath = folderPath;
        CurrentFolderName = folderName;

        using (PerfSpan.Begin("Playlist.ScanVideoFiles", new Dictionary<string, string>
        {
            ["folder"] = folderName
        }))
        {
            VideoFiles = _videoScanner.GetVideoFiles(folderPath);
        }

        Log.Info($"Scanned {VideoFiles.Length} video files");

        using (PerfSpan.Begin("Playlist.BuildSkeletonItems", new Dictionary<string, string>
        {
            ["folder"] = folderName,
            ["count"] = VideoFiles.Length.ToString()
        }))
        {
            Items.Clear();
            for (int i = 0; i < VideoFiles.Length; i++)
            {
                var filePath = VideoFiles[i];
                Items.Add(new PlaylistItem
                {
                    Number = i + 1,
                    Title = Path.GetFileName(filePath),
                    FilePath = filePath
                });
            }
        }

        CurrentIndex = -1;
        PreloadFolderData();
    }

    public void PreloadFolderData()
    {
        if (VideoFiles.Length == 0)
        {
            _preloadTask = null;
            return;
        }

        _preloadTask = Task.Run(() =>
        {
            var playedStates = new bool[VideoFiles.Length];
            var thumbStates = new bool[VideoFiles.Length];
            for (int i = 0; i < VideoFiles.Length; i++)
            {
                playedStates[i] = _settings.IsVideoPlayed(VideoFiles[i]);
                thumbStates[i] = _getThumbnailState(VideoFiles[i]) == ThumbnailState.Ready;
            }
            return (playedStates, thumbStates);
        });
    }

    public async Task LoadFolderDataAsync()
    {
        if (VideoFiles.Length == 0)
        {
            LoadFolderData();
            return;
        }

        bool[] playedStates;
        bool[] thumbStates;

        if (_preloadTask != null)
        {
            (playedStates, thumbStates) = await _preloadTask;
            _preloadTask = null;
        }
        else
        {
            playedStates = new bool[VideoFiles.Length];
            thumbStates = new bool[VideoFiles.Length];
            for (int i = 0; i < VideoFiles.Length; i++)
            {
                playedStates[i] = _settings.IsVideoPlayed(VideoFiles[i]);
                thumbStates[i] = _getThumbnailState(VideoFiles[i]) == ThumbnailState.Ready;
            }
        }

        for (int i = 0; i < VideoFiles.Length; i++)
        {
            Items[i].IsPlayed = playedStates[i];
            Items[i].IsThumbnailReady = thumbStates[i];
        }

        LoadFolderData();
    }

    private void LoadFolderData()
    {
        using (PerfSpan.Begin("Playlist.ResolveStartVideo", new Dictionary<string, string>
        {
            ["folder"] = CurrentFolderName
        }))
        {
            var folderProgress = _settings.GetFolderProgress(CurrentFolderPath);
            string? targetVideo = folderProgress?.LastVideoPath;

            if (string.IsNullOrEmpty(targetVideo) || !File.Exists(targetVideo))
                targetVideo = VideoFiles.Length > 0 ? VideoFiles[0] : null;

            if (!string.IsNullOrEmpty(targetVideo))
            {
                int index = Array.IndexOf(VideoFiles, targetVideo);
                CurrentIndex = index >= 0 ? index : 0;
            }
            else
            {
                CurrentIndex = VideoFiles.Length > 0 ? 0 : -1;
            }
        }
    }

    public void PlayCurrentVideo()
    {
        if (CurrentIndex < 0 || CurrentIndex >= VideoFiles.Length) return;

        using var playSpan = PerfSpan.Begin("Playlist.PlayCurrentVideo", new Dictionary<string, string>
        {
            ["folder"] = CurrentFolderName,
            ["index"] = CurrentIndex.ToString()
        });

        string filePath = VideoFiles[CurrentIndex];

        long startTime = 0;
        var progress = _settings.GetVideoProgress(filePath);
        if (progress != null)
        {
            startTime = progress.Position;
            if (progress.Duration > 0 && startTime > progress.Duration * 0.9)
                startTime = 0;
        }

        _media.Play(filePath, startTime);
        _settings.SetFolderProgress(CurrentFolderPath, filePath);
        _settings.MarkVideoPlayed(filePath);

        VideoPlayed?.Invoke(filePath);
    }

    public bool PlayNext()
    {
        if (CurrentIndex >= VideoFiles.Length - 1) return false;
        SaveProgress();
        if (CurrentIndex >= 0 && CurrentIndex < Items.Count)
            Items[CurrentIndex].IsPlayed = true;
        CurrentIndex++;
        PlayCurrentVideo();
        return true;
    }

    public bool PlayPrevious()
    {
        if (CurrentIndex <= 0) return false;
        SaveProgress();
        if (CurrentIndex >= 0 && CurrentIndex < Items.Count)
            Items[CurrentIndex].IsPlayed = true;
        CurrentIndex--;
        PlayCurrentVideo();
        return true;
    }

    public void PlayEpisode(int index)
    {
        if (index < 0 || index >= VideoFiles.Length) return;
        SaveProgress();
        CurrentIndex = index;
        PlayCurrentVideo();
    }

    public void SaveProgress()
    {
        string? filePath = _media.CurrentFilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        long time = _media.Time;
        long length = _media.Length;
        if (length > 0)
            _settings.SetVideoProgress(filePath, time, length);
    }

    public void UpdateThumbnailReady(string videoPath)
    {
        foreach (var item in Items)
        {
            if (string.Equals(item.FilePath, videoPath, StringComparison.OrdinalIgnoreCase))
            {
                item.IsThumbnailReady = true;
                return;
            }
        }
    }

    public void UpdateThumbnailProgress(string videoPath, int percent)
    {
        foreach (var item in Items)
        {
            if (string.Equals(item.FilePath, videoPath, StringComparison.OrdinalIgnoreCase))
            {
                item.ThumbnailProgress = percent;
                return;
            }
        }
    }
}





