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

    // Query
    ThumbnailGenerationStatusSnapshot GetStatusSnapshot();
    ThumbnailState GetThumbnailState(string videoPath);
    byte[]? GetThumbnailBytes(string videoPath, long positionMs);

    // Collection-oriented commands
    void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths);
    void RemoveCollection(string collectionId);
    void DeleteCollection(string collectionId, IReadOnlyCollection<string>? videoPaths = null);
    void FocusCollection(string collectionId);
    void BoostCollection(string collectionId);
    void ResetCollection(string collectionId, bool boostAfterReset);

    // Playback-oriented commands
    void BoostVideo(string videoPath);
    void BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount);

    // Runtime controls
    void SetPlayerActive(bool isActive);
    void RefreshPerformanceMode();
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
    string? CurrentTargetIntent,
    IReadOnlyList<ThumbnailActiveTaskSnapshot> ActiveTasks);

public sealed record ThumbnailActiveTaskSnapshot(
    string VideoPath,
    string VideoName,
    ThumbnailWorkIntent Intent,
    ThumbnailState State,
    int ProgressPercent,
    bool IsForeground,
    bool IsSuspended);
