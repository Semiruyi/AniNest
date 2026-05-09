using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailGenerationRunner
{
    private static readonly Logger Log = AppLog.For<ThumbnailGenerationRunner>();

    private readonly IThumbnailDecodeStrategyService _decodeStrategyService;
    private readonly ThumbnailRenderer _renderer;

    public ThumbnailGenerationRunner(
        IThumbnailDecodeStrategyService decodeStrategyService,
        ThumbnailRenderer renderer)
    {
        _decodeStrategyService = decodeStrategyService;
        _renderer = renderer;
    }

    public async Task<RenderResult> GenerateAsync(
        ThumbnailTask task,
        CancellationToken ct,
        Action<string, int>? progressCallback = null)
    {
        IReadOnlyList<ThumbnailDecodeStrategy> strategies = _decodeStrategyService.GetStrategyChain();
        RenderResult lastResult = new(ThumbnailState.Failed);

        foreach (ThumbnailDecodeStrategy strategy in strategies)
        {
            ct.ThrowIfCancellationRequested();
            Log.Info($"Thumbnail render attempt: file={Path.GetFileName(task.VideoPath)}, strategy={strategy}");
            lastResult = await _renderer.GenerateAsync(task, strategy, ct, progressCallback);

            if (lastResult.State == ThumbnailState.Ready)
            {
                _decodeStrategyService.RecordSuccess(strategy);
                Log.Info(
                    $"Thumbnail render success: file={Path.GetFileName(task.VideoPath)}, " +
                    $"strategy={strategy}, frames={lastResult.FrameCount}");
                return lastResult;
            }

            Log.Info($"Thumbnail render fallback: file={Path.GetFileName(task.VideoPath)}, strategy={strategy}, result={lastResult.State}");
        }

        Log.Warning(
            $"Thumbnail render failed all strategies: file={Path.GetFileName(task.VideoPath)}, " +
            $"attempts={string.Join(" -> ", strategies)}");
        return lastResult;
    }
}
