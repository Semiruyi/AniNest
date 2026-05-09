using System;
using System.Collections.Generic;

namespace AniNest.Infrastructure.Thumbnails;

public enum LibraryCollectionKind
{
    Folder,
    SystemCategory,
    UserCategory,
    Virtual
}

public sealed record LibraryCollectionRef(
    string Id,
    LibraryCollectionKind Kind,
    string Name);

public enum ThumbnailWorkIntent
{
    BackgroundFill,
    FocusedCollection,
    ManualCollection,
    PlaybackNearby,
    PlaybackCurrent,
    ManualSingle
}

public interface IThumbnailGenerator
{
    bool IsFfmpegAvailable { get; }
    ThumbnailGenerationStatusSnapshot GetStatusSnapshot();

    ThumbnailState GetState(string videoPath);
    byte[]? GetThumbnailBytes(string videoPath, long positionMs);

    void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths);
    void RemoveCollection(string collectionId);
    void FocusCollection(string collectionId);
    void BoostCollection(string collectionId);
    void BoostVideo(string videoPath);
    void BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount);

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
    int PendingCount,
    int ForegroundPendingCount,
    string? CurrentTargetName,
    string? CurrentTargetIntent);
