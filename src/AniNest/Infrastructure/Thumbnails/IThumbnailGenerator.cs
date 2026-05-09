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
    ThumbnailGenerationStatusSnapshot GetStatusSnapshot();

    ThumbnailState GetState(string videoPath);
    byte[]? GetThumbnailBytes(string videoPath, long positionMs);

    void EnqueueFolder(string folderPath, IReadOnlyCollection<string> videoFiles, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths);
    void DeleteForFolder(string folderPath, IReadOnlyCollection<string>? videoFiles = null);
    void SetPlayerActive(bool isActive);
    void RefreshPerformanceMode();
    void RefreshGenerationPaused();
    void RefreshDecodeStrategy();

    void Shutdown();

    event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    event Action<string, int>? VideoProgress;
    event Action<string>? VideoReady;
    event Action? StatusChanged;
}

public sealed record ThumbnailGenerationStatusSnapshot(
    bool IsPaused,
    bool IsPlayerActive,
    int ActiveWorkers,
    int ReadyCount,
    int TotalCount,
    int PendingCount);



