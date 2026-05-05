using System;
using System.Collections.Generic;

namespace LocalPlayer.Infrastructure.Model;

public interface IThumbnailGenerator
{
    bool IsFfmpegAvailable { get; }

    ThumbnailState GetState(string videoPath);
    string? GetThumbnailPath(string videoPath, int second);

    void EnqueueFolder(string folderPath, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths);
    void DeleteForFolder(string folderPath);

    void Shutdown();

    event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    event Action<string, int>? VideoProgress;
    event Action<string>? VideoReady;
}

