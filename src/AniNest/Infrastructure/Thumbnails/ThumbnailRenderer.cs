using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
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
    public bool UsedKeyframesOnly { get; }
    public RenderResult(ThumbnailState state, int frameCount = 0, bool usedKeyframesOnly = false)
    {
        State = state;
        FrameCount = frameCount;
        UsedKeyframesOnly = usedKeyframesOnly;
    }
}

internal class ThumbnailRenderer
{
    private static readonly Logger Log = AppLog.For<ThumbnailRenderer>();
    private const double MinSamplingIntervalSeconds = 0.5;
    private const double MaxSamplingIntervalSeconds = 5.0;
    private const double SamplingIntervalDivisor = 1200.0;

    private readonly string _ffmpegPath;
    private readonly string _thumbBaseDir;
    private readonly Func<string, double> _getDuration;
    private readonly string _ffprobePath;

    public ThumbnailRenderer(string ffmpegPath, string thumbBaseDir, Func<string, double> getDuration)
    {
        _ffmpegPath = ffmpegPath;
        _thumbBaseDir = thumbBaseDir;
        _getDuration = getDuration;
        _ffprobePath = ResolveFfprobePath(ffmpegPath);
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
        bool useKeyframeOnlyExtraction = ShouldUseKeyframeOnlyExtraction(task.VideoPath);
        var generatedFrameSeconds = useKeyframeOnlyExtraction ? new List<int>() : null;
        Log.Info(
            $"Thumbnail render begin: file={Path.GetFileName(task.VideoPath)}, duration={totalSec:F1}s, tmpDir={tmpDir}, keyframesOnly={useKeyframeOnlyExtraction}");

        string args = useKeyframeOnlyExtraction
            ? $"{BuildInputArguments(strategy)}-skip_frame nokey -y -i \"{task.VideoPath}\" " +
              "-vf \"scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease,showinfo\" " +
              $"-vsync vfr -q:v 5 \"{tmpDir}\\%04d.jpg\""
            : $"{BuildInputArguments(strategy)}-y -i \"{task.VideoPath}\" " +
              $"-vf \"fps={BuildSamplingFpsExpression(totalSec)},scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease\" " +
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
                        if (useKeyframeOnlyExtraction &&
                            TryParseShowInfoSecond(line, out int frameSecond) &&
                            generatedFrameSeconds != null)
                        {
                            generatedFrameSeconds.Add(frameSecond);
                        }

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
                if (Directory.Exists(finalDir))
                    Directory.Delete(finalDir, recursive: true);

                int frameCount = 0;
                if (Directory.Exists(tmpDir))
                {
                    if (useKeyframeOnlyExtraction)
                    {
                        frameCount = NormalizeKeyframeOutput(tmpDir, finalDir, generatedFrameSeconds ?? []);
                    }
                    else
                    {
                        frameCount = Directory.GetFiles(tmpDir, "*.jpg").Length;
                        Directory.CreateDirectory(finalDir);
                        SaveSampledFrameIndex(tmpDir, totalSec, frameCount);
                        File.Move(ThumbnailFrameIndex.GetIndexPath(tmpDir), ThumbnailFrameIndex.GetIndexPath(finalDir), overwrite: true);
                        ThumbnailBundle.Write(tmpDir, finalDir);
                        Directory.Delete(tmpDir, recursive: true);
                    }
                }

                Log.Info(
                    $"Completed: {Path.GetFileName(task.VideoPath)}, {frameCount} frames");
                Log.Info(MemorySnapshot.Capture("ThumbnailRenderer.GenerateAsync.completed",
                    ("file", Path.GetFileName(task.VideoPath)),
                    ("pid", process.Id),
                    ("strategy", strategy.ToString()),
                    ("frames", frameCount)));
                return new RenderResult(ThumbnailState.Ready, frameCount, useKeyframeOnlyExtraction);
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

    internal static double CalculateSamplingIntervalSeconds(double durationSeconds)
    {
        if (durationSeconds <= 0)
            return 1.0;

        double intervalSeconds = durationSeconds / SamplingIntervalDivisor;
        return Math.Clamp(intervalSeconds, MinSamplingIntervalSeconds, MaxSamplingIntervalSeconds);
    }

    internal static string BuildSamplingFpsExpression(double durationSeconds)
    {
        double intervalSeconds = CalculateSamplingIntervalSeconds(durationSeconds);
        double fps = 1.0 / intervalSeconds;
        return fps.ToString("0.######", CultureInfo.InvariantCulture);
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

    private bool ShouldUseKeyframeOnlyExtraction(string videoPath)
    {
        string extension = Path.GetExtension(videoPath);
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        extension = extension.ToLowerInvariant();
        if (extension is not ".mp4" and not ".mkv" and not ".mov" and not ".ts" and not ".m4v")
            return false;

        string? codecName = ProbeVideoCodec(videoPath);
        return string.Equals(codecName, "h264", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(codecName, "hevc", StringComparison.OrdinalIgnoreCase);
    }

    private string? ProbeVideoCodec(string videoPath)
    {
        if (!File.Exists(_ffprobePath))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveFfprobePath(string ffmpegPath)
    {
        string directory = Path.GetDirectoryName(ffmpegPath) ?? string.Empty;
        string ffprobePath = Path.Combine(directory, "ffprobe.exe");
        return ffprobePath;
    }

    internal static IReadOnlyList<long> BuildSampledFramePositionsMs(double durationSeconds, int frameCount)
    {
        if (frameCount <= 0)
            return Array.Empty<long>();

        double intervalSeconds = CalculateSamplingIntervalSeconds(durationSeconds);
        var framePositionsMs = new List<long>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            double positionSeconds = i * intervalSeconds;
            long positionMs = positionSeconds <= 0
                ? 0
                : (long)Math.Round(positionSeconds * 1000.0, MidpointRounding.AwayFromZero);
            framePositionsMs.Add(positionMs);
        }

        return framePositionsMs;
    }

    private static void SaveSampledFrameIndex(string thumbnailDirectory, double durationSeconds, int frameCount)
    {
        IReadOnlyList<long> framePositionsMs = BuildSampledFramePositionsMs(durationSeconds, frameCount);
        if (framePositionsMs.Count == 0)
            return;

        ThumbnailFrameIndex.Save(thumbnailDirectory, framePositionsMs);
    }

    private static int NormalizeKeyframeOutput(string tmpDir, string finalDir, IReadOnlyList<int> generatedFrameSeconds)
    {
        var frameFiles = Directory.GetFiles(tmpDir, "*.jpg")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameFiles.Length == 0)
            return 0;

        var framePositionsMs = new List<long>(frameFiles.Length);
        Directory.CreateDirectory(finalDir);

        for (int i = 0; i < frameFiles.Length; i++)
        {
            string sourcePath = frameFiles[i];
            int second = i < generatedFrameSeconds.Count
                ? Math.Max(0, generatedFrameSeconds[i])
                : i;
            framePositionsMs.Add(second * 1000L);

            string destinationPath = Path.Combine(finalDir, $"{i + 1:D4}.jpg");
            File.Move(sourcePath, destinationPath, overwrite: true);
        }

        ThumbnailFrameIndex.Save(finalDir, framePositionsMs);
        Directory.Delete(tmpDir, recursive: true);
        return frameFiles.Length;
    }

    private static bool TryParseShowInfoSecond(string line, out int second)
    {
        second = 0;
        int markerIndex = line.IndexOf("pts_time:", StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        int start = markerIndex + "pts_time:".Length;
        int end = start;
        while (end < line.Length && !char.IsWhiteSpace(line[end]))
            end++;

        string value = line[start..end];
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            return false;

        second = parsed <= 0
            ? 0
            : parsed >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
        return true;
    }
}



