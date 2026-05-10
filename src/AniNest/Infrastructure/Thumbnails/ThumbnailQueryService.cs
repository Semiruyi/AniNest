using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AniNest.Infrastructure.Diagnostics;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailQueryService
{
    private readonly ThumbnailTaskStore _taskStore;
    private readonly ThumbnailStatusTracker _statusTracker;
    private readonly ThumbnailWorkerPool _workerPool;
    private readonly string _thumbBaseDir;
    private readonly Func<bool> _isGenerationPaused;
    private readonly Func<bool> _isPlayerActive;

    public ThumbnailQueryService(
        ThumbnailTaskStore taskStore,
        ThumbnailStatusTracker statusTracker,
        ThumbnailWorkerPool workerPool,
        string thumbBaseDir,
        Func<bool> isGenerationPaused,
        Func<bool> isPlayerActive)
    {
        _taskStore = taskStore;
        _statusTracker = statusTracker;
        _workerPool = workerPool;
        _thumbBaseDir = thumbBaseDir;
        _isGenerationPaused = isGenerationPaused;
        _isPlayerActive = isPlayerActive;
    }

    public ThumbnailGenerationStatusSnapshot GetStatusSnapshot()
        => _statusTracker.CreateSnapshot(_isGenerationPaused(), _isPlayerActive(), _workerPool.Count);

    public ThumbnailState GetState(string videoPath)
    {
        using var span = PerfSpan.Begin("Thumbnail.GetState", new Dictionary<string, string>
        {
            ["file"] = Path.GetFileName(videoPath)
        });
        return _taskStore.GetState(videoPath);
    }

    public byte[]? GetThumbnailBytes(string videoPath, long positionMs, bool isFfmpegAvailable)
    {
        if (!isFfmpegAvailable)
            return null;

        _taskStore.TryGetTask(videoPath, out var task);

        if (task == null || task.State != ThumbnailState.Ready)
            return null;

        string directory = Path.Combine(_thumbBaseDir, task.Md5Dir);
        int? frameIndex = ThumbnailFrameIndex.ResolveFrameIndex(directory, positionMs);
        if (frameIndex == null)
            return null;

        return ThumbnailBundle.ReadFrameBytes(directory, frameIndex.Value);
    }
}
