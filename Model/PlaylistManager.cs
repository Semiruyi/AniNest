using System;
using System.Collections.Generic;
using System.IO;
using LocalPlayer.Media;

namespace LocalPlayer.Model;

public class PlaylistManager
{
    private static void Log(string message) => AppLog.Info(nameof(PlaylistManager), message);

    private readonly ISettingsService _settings;
    private readonly IMediaPlayerController _media;
    private readonly Func<string, ThumbnailState> _getThumbnailState;

    public string CurrentFolderPath { get; private set; } = "";
    public string CurrentFolderName { get; private set; } = "";
    public string[] VideoFiles { get; private set; } = Array.Empty<string>();
    public List<PlaylistItem> Items { get; private set; } = new();
    public int CurrentIndex { get; set; } = -1;

    public PlaylistItem? CurrentItem =>
        CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex] : null;

    public int ItemCount => Items.Count;

    public event Action<string>? VideoPlayed;

    public PlaylistManager(ISettingsService settings, IMediaPlayerController media,
                           Func<string, ThumbnailState> getThumbnailState)
    {
        _settings = settings;
        _media = media;
        _getThumbnailState = getThumbnailState;
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        CurrentFolderPath = folderPath;
        CurrentFolderName = folderName;

        VideoFiles = VideoScanner.GetVideoFiles(folderPath);
        Log($"扫描到 {VideoFiles.Length} 个视频文件");

        Items = new List<PlaylistItem>();
        for (int i = 0; i < VideoFiles.Length; i++)
        {
            var filePath = VideoFiles[i];
            Items.Add(new PlaylistItem
            {
                Number = i + 1,
                Title = Path.GetFileName(filePath),
                FilePath = filePath,
                IsPlayed = _settings.IsVideoPlayed(filePath),
                IsThumbnailReady = _getThumbnailState(filePath) == ThumbnailState.Ready
            });
        }

        var folderProgress = _settings.GetFolderProgress(folderPath);
        string? targetVideo = folderProgress?.LastVideoPath;

        if (string.IsNullOrEmpty(targetVideo) || !File.Exists(targetVideo))
            targetVideo = VideoFiles.Length > 0 ? VideoFiles[0] : null;

        if (!string.IsNullOrEmpty(targetVideo))
        {
            int index = Array.IndexOf(VideoFiles, targetVideo);
            if (index >= 0)
                CurrentIndex = index;
            else
                PlayCurrentVideo();
        }
        else
        {
            CurrentIndex = -1;
        }
    }

    public void PlayCurrentVideo()
    {
        if (CurrentIndex < 0 || CurrentIndex >= VideoFiles.Length) return;

        string filePath = VideoFiles[CurrentIndex];
        Log($"[PlayVideo] 开始: {Path.GetFileName(filePath)}");

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
                break;
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
                break;
            }
        }
    }
}
