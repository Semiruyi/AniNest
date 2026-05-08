using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Thumbnails;

internal readonly struct RenderResult
{
    public ThumbnailState State { get; }
    public int FrameCount { get; }
    public RenderResult(ThumbnailState state, int frameCount = 0)
    {
        State = state;
        FrameCount = frameCount;
    }
}

internal class ThumbnailRenderer
{
    private static readonly Logger Log = AppLog.For<ThumbnailRenderer>();

    private readonly string _ffmpegPath;
    private readonly string _thumbBaseDir;
    private readonly Func<string, double> _getDuration;

    public ThumbnailRenderer(string ffmpegPath, string thumbBaseDir, Func<string, double> getDuration)
    {
        _ffmpegPath = ffmpegPath;
        _thumbBaseDir = thumbBaseDir;
        _getDuration = getDuration;
    }

    public async Task<RenderResult> GenerateAsync(
        ThumbnailTask task,
        ThumbnailDecodeStrategy strategy,
        CancellationToken ct,
        Action<string, int>? onProgress = null)
    {
        string tmpDir = Path.Combine(_thumbBaseDir, $".tmp_{task.Md5Dir}");
        string finalDir = Path.Combine(_thumbBaseDir, task.Md5Dir);

        try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }

        if (!File.Exists(task.VideoPath))
        {
            Log.Info(
                $"Video file missing: {task.VideoPath}");
            return new RenderResult(ThumbnailState.Failed);
        }

        Directory.CreateDirectory(tmpDir);

        double totalSec = GetVideoDuration(task.VideoPath);
        Log.Info(
            $"Thumbnail render begin: file={Path.GetFileName(task.VideoPath)}, duration={totalSec:F1}s, tmpDir={tmpDir}");

        string args = $"{BuildInputArguments(strategy)}-y -i \"{task.VideoPath}\" " +
            "-vf \"fps=1,scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease\" " +
            $"-q:v 5 \"{tmpDir}\\%04d.jpg\"";

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            ct.ThrowIfCancellationRequested();
            process.Start();
            Log.Info(MemorySnapshot.Capture("ThumbnailRenderer.GenerateAsync.process-start",
                ("file", Path.GetFileName(task.VideoPath)),
                ("pid", process.Id),
                ("strategy", strategy.ToString()),
                ("tmpDir", Path.GetFileName(tmpDir))));

            int lastPercent = -1;
            var stderrTask = Task.Run(() =>
            {
                try
                {
                    string? line;
                    while ((line = process.StandardError.ReadLine()) != null)
                    {
                        if (totalSec <= 0) continue;
                        int ti = line.IndexOf("time=", StringComparison.Ordinal);
                        if (ti < 0) continue;

                        int end = line.IndexOf(' ', ti + 5);
                        string timeStr = end > ti
                            ? line.Substring(ti + 5, end - ti - 5).Trim()
                            : line.Substring(ti + 5).Trim();
                        if (!TimeSpan.TryParse(timeStr, out var ts)) continue;

                        int percent = (int)(ts.TotalSeconds / totalSec * 100);
                        if (percent > lastPercent && percent <= 100)
                        {
                            lastPercent = percent;
                            onProgress?.Invoke(task.VideoPath, percent);
                        }
                    }
                }
                catch { }
            }, ct);

            await process.WaitForExitAsync(ct);
            Log.Info(
                $"Thumbnail renderer wait-exit completed; waiting stderr task, pid={process.Id}");
            await stderrTask;
            Log.Info(
                $"Thumbnail renderer stderr task completed, pid={process.Id}");

            int exitCode = process.ExitCode;
            Log.Info(MemorySnapshot.Capture("ThumbnailRenderer.GenerateAsync.process-exit",
                ("file", Path.GetFileName(task.VideoPath)),
                ("pid", process.Id),
                ("strategy", strategy.ToString()),
                ("exitCode", exitCode)));
            Log.Info(
                $"Thumbnail renderer process exited: exitCode={exitCode}, file={Path.GetFileName(task.VideoPath)}");

            if (exitCode == 0)
            {
                int frameCount = 0;
                if (Directory.Exists(tmpDir))
                    frameCount = Directory.GetFiles(tmpDir, "*.jpg").Length;

                if (Directory.Exists(finalDir))
                    Directory.Delete(finalDir, recursive: true);
                Directory.Move(tmpDir, finalDir);

                Log.Info(
                    $"Completed: {Path.GetFileName(task.VideoPath)}, {frameCount} frames");
                Log.Info(MemorySnapshot.Capture("ThumbnailRenderer.GenerateAsync.completed",
                    ("file", Path.GetFileName(task.VideoPath)),
                    ("pid", process.Id),
                    ("strategy", strategy.ToString()),
                    ("frames", frameCount)));
                return new RenderResult(ThumbnailState.Ready, frameCount);
            }
            else
            {
                Log.Info(
                    $"Failed: {Path.GetFileName(task.VideoPath)}, ExitCode={exitCode}");
                return new RenderResult(ThumbnailState.Failed);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Info(
                $"Canceled: HasExited={process.HasExited}, PID={process.Id}");
            var killSw = Stopwatch.StartNew();
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            killSw.Stop();
            Log.Info(
                $"Thumbnail renderer process kill completed: elapsed={killSw.ElapsedMilliseconds}ms, pid={process.Id}");
            throw;
        }
        finally
        {
            var cleanSw = Stopwatch.StartNew();
            try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }
            cleanSw.Stop();
            Log.Info(
                $"Thumbnail renderer cleanup completed: elapsed={cleanSw.ElapsedMilliseconds}ms, pid={process.Id}");
        }
    }

    public double GetVideoDuration(string videoPath)
    {
        try
        {
            double sec = _getDuration(videoPath);
            if (sec <= 0)
                return 0;
            return sec;
        }
        catch (Exception ex)
        {
            Log.Error("Get video duration failed", ex);
            return 0;
        }
    }

    private static string BuildInputArguments(ThumbnailDecodeStrategy strategy)
        => strategy switch
        {
            ThumbnailDecodeStrategy.NvidiaCuda => "-hwaccel cuda ",
            ThumbnailDecodeStrategy.IntelQsv => "-hwaccel qsv ",
            ThumbnailDecodeStrategy.D3D11VA => "-hwaccel d3d11va ",
            ThumbnailDecodeStrategy.AutoHardware => "-hwaccel auto ",
            _ => string.Empty
        };
}



