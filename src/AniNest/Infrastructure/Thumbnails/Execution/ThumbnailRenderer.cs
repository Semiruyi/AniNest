using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Buffers;
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
    internal static Action<string, string>? TestDirectoryMoveOverride;
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
        Action<string, int>? onProgress = null,
        Action<int>? processStartedCallback = null)
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
              "-vsync vfr -q:v 5 -f image2pipe -vcodec mjpeg pipe:1"
            : $"{BuildInputArguments(strategy)}-y -i \"{task.VideoPath}\" " +
              $"-vf \"fps={BuildSamplingFpsExpression(totalSec)},scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease\" " +
              "-q:v 5 -f image2pipe -vcodec mjpeg pipe:1";

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            ct.ThrowIfCancellationRequested();
            process.Start();
            processStartedCallback?.Invoke(process.Id);
            Log.Info(MemorySnapshot.Capture("ThumbnailRenderer.GenerateAsync.process-start",
                ("file", Path.GetFileName(task.VideoPath)),
                ("pid", process.Id),
                ("strategy", strategy.ToString()),
                ("tmpDir", Path.GetFileName(tmpDir))));

            using var bundleWriter = ThumbnailBundle.CreateWriter(tmpDir);

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

            int frameCount = useKeyframeOnlyExtraction
                ? await ReadKeyframeBundleAsync(process.StandardOutput.BaseStream, bundleWriter, ct)
                : await ReadSampledBundleAsync(process.StandardOutput.BaseStream, bundleWriter, totalSec, ct);

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
                if (useKeyframeOnlyExtraction && generatedFrameSeconds != null)
                {
                    for (int i = 0; i < frameCount; i++)
                    {
                        int second = i < generatedFrameSeconds.Count
                            ? Math.Max(0, generatedFrameSeconds[i])
                            : i;
                        bundleWriter.UpdateFramePosition(i, second * 1000L);
                    }
                }

                bundleWriter.Commit();

                PromoteRenderedDirectory(tmpDir, finalDir);

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

        var framePositionsMs = new List<long>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            framePositionsMs.Add(GetSampledFramePositionMs(durationSeconds, i));
        }

        return framePositionsMs;
    }

    internal static long GetSampledFramePositionMs(double durationSeconds, int frameIndex)
    {
        if (frameIndex <= 0)
            return 0;

        double intervalSeconds = CalculateSamplingIntervalSeconds(durationSeconds);
        double positionSeconds = frameIndex * intervalSeconds;
        return (long)Math.Round(positionSeconds * 1000.0, MidpointRounding.AwayFromZero);
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

    internal static async Task ReadJpegFramesAsync(
        Stream stream,
        Action<byte[]> onFrame,
        CancellationToken cancellationToken)
    {
        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        byte[] carryBuffer = Array.Empty<byte>();
        int carryLength = 0;

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken);
                if (read <= 0)
                    break;

                int combinedLength = carryLength + read;
                byte[] combinedBuffer = ArrayPool<byte>.Shared.Rent(combinedLength);

                try
                {
                    if (carryLength > 0)
                        Buffer.BlockCopy(carryBuffer, 0, combinedBuffer, 0, carryLength);

                    Buffer.BlockCopy(readBuffer, 0, combinedBuffer, carryLength, read);

                    int searchStart = 0;
                    while (TryExtractJpegFrame(combinedBuffer, combinedLength, ref searchStart, out byte[]? frame))
                    {
                        if (frame != null)
                            onFrame(frame);
                    }

                    int remainingLength = combinedLength - searchStart;
                    if (remainingLength <= 0)
                    {
                        carryLength = 0;
                        continue;
                    }

                    if (carryBuffer.Length < remainingLength)
                        carryBuffer = new byte[Math.Max(remainingLength, 2)];

                    Buffer.BlockCopy(combinedBuffer, searchStart, carryBuffer, 0, remainingLength);
                    carryLength = remainingLength;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(combinedBuffer);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    internal static bool TryExtractJpegFrame(
        byte[] buffer,
        int length,
        ref int searchStart,
        out byte[]? frame)
    {
        frame = null;
        int start = -1;

        for (int i = searchStart; i < length - 1; i++)
        {
            if (start < 0)
            {
                if (buffer[i] == 0xFF && buffer[i + 1] == 0xD8)
                {
                    start = i;
                    i++;
                }

                continue;
            }

            if (buffer[i] == 0xFF && buffer[i + 1] == 0xD9)
            {
                int frameLength = i + 2 - start;
                frame = new byte[frameLength];
                Buffer.BlockCopy(buffer, start, frame, 0, frameLength);
                searchStart = i + 2;
                return true;
            }
        }

        if (start >= 0)
        {
            searchStart = start;
        }
        else
        {
            searchStart = Math.Max(0, length - 1);
        }

        return false;
    }

    private static async Task<int> ReadSampledBundleAsync(
        Stream outputStream,
        ThumbnailBundle.BundleWriter bundleWriter,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        int frameIndex = 0;
        await ReadJpegFramesAsync(outputStream, frameBytes =>
        {
            bundleWriter.AppendFrame(GetSampledFramePositionMs(durationSeconds, frameIndex), frameBytes);
            frameIndex++;
        }, cancellationToken);

        return frameIndex;
    }

    private static async Task<int> ReadKeyframeBundleAsync(
        Stream outputStream,
        ThumbnailBundle.BundleWriter bundleWriter,
        CancellationToken cancellationToken)
    {
        int frameIndex = 0;
        await ReadJpegFramesAsync(outputStream, frameBytes =>
        {
            bundleWriter.AppendFrame(frameIndex * 1000L, frameBytes);
            frameIndex++;
        }, cancellationToken);

        return frameIndex;
    }

    internal static void PromoteRenderedDirectory(string stagedDirectory, string finalDirectory)
    {
        if (!Directory.Exists(stagedDirectory))
            return;

        if (!Directory.Exists(finalDirectory))
        {
            MoveDirectory(stagedDirectory, finalDirectory);
            return;
        }

        string backupDirectory = finalDirectory + ".bak";
        if (Directory.Exists(backupDirectory))
            Directory.Delete(backupDirectory, recursive: true);

        MoveDirectory(finalDirectory, backupDirectory);

        try
        {
            MoveDirectory(stagedDirectory, finalDirectory);
            Directory.Delete(backupDirectory, recursive: true);
        }
        catch
        {
            if (!Directory.Exists(finalDirectory) && Directory.Exists(backupDirectory))
                MoveDirectory(backupDirectory, finalDirectory);

            throw;
        }
    }

    private static void MoveDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (TestDirectoryMoveOverride != null)
        {
            TestDirectoryMoveOverride(sourceDirectory, destinationDirectory);
            return;
        }

        Directory.Move(sourceDirectory, destinationDirectory);
    }
}



