using System;
using System.Collections.Generic;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Thumbnails;

public interface IThumbnailGenerator
{
    bool IsFfmpegAvailable { get; }

    ThumbnailState GetState(string videoPath);
    string? GetThumbnailPath(string videoPath, int second);

    void EnqueueFolder(string folderPath, IReadOnlyCollection<string> videoFiles, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths);
    void DeleteForFolder(string folderPath, IReadOnlyCollection<string>? videoFiles = null);

    void Shutdown();

    event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    event Action<string, int>? VideoProgress;
    event Action<string>? VideoReady;
}



