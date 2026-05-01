using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalPlayer.Model;

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

/// <summary>
/// ffmpeg 进程执行器：为单个视频生成逐秒缩略图。
/// 完全拥有 Process 生命周期 — 创建、启动、取消时 Kill + 清理 tmp。
/// 只读 task（VideoPath / Md5Dir），绝不写入 task.State。
/// </summary>
internal class ThumbnailRenderer
{
    private static readonly Logger Log = AppLog.For<ThumbnailRenderer>();

    private readonly string _ffmpegPath;
    private readonly string _thumbBaseDir;

    public ThumbnailRenderer(string ffmpegPath, string thumbBaseDir)
    {
        _ffmpegPath = ffmpegPath;
        _thumbBaseDir = thumbBaseDir;
    }

    public async Task<RenderResult> GenerateAsync(
        ThumbnailTask task, CancellationToken ct,
        Action<string, int>? onProgress = null)
    {
        string tmpDir = Path.Combine(_thumbBaseDir, $".tmp_{task.Md5Dir}");
        string finalDir = Path.Combine(_thumbBaseDir, task.Md5Dir);

        // 清理残留 tmp 目录
        try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }

        if (!File.Exists(task.VideoPath))
        {
            Log.Info(
                $"视频文件不存在: {task.VideoPath}");
            return new RenderResult(ThumbnailState.Failed);
        }

        Directory.CreateDirectory(tmpDir);

        double totalSec = GetVideoDuration(task.VideoPath);
        Log.Info(
            $"开始: {Path.GetFileName(task.VideoPath)}, 时长={totalSec:F1}s, tmpDir={tmpDir}");

        string args = $"-y -i \"{task.VideoPath}\" " +
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
            Log.Info( $"ffmpeg 进程已启动, PID={process.Id}");

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
                $"WaitForExit 完成, 等待 stderrTask, PID={process.Id}");
            await stderrTask;
            Log.Info(
                $"stderrTask 完成, PID={process.Id}");

            int exitCode = process.ExitCode;
            Log.Info(
                $"ffmpeg 退出, ExitCode={exitCode}, 视频={Path.GetFileName(task.VideoPath)}");

            if (exitCode == 0)
            {
                int frameCount = 0;
                if (Directory.Exists(tmpDir))
                    frameCount = Directory.GetFiles(tmpDir, "*.jpg").Length;

                if (Directory.Exists(finalDir))
                    Directory.Delete(finalDir, recursive: true);
                Directory.Move(tmpDir, finalDir);

                Log.Info(
                    $"完成: {Path.GetFileName(task.VideoPath)}, {frameCount} 帧");
                return new RenderResult(ThumbnailState.Ready, frameCount);
            }
            else
            {
                Log.Info(
                    $"失败: {Path.GetFileName(task.VideoPath)}, ExitCode={exitCode}");
                return new RenderResult(ThumbnailState.Failed);
            }
        }
        catch (OperationCanceledException)
        {
            // 取消：杀进程、清 tmp、重抛给 Generator 设 State = Pending
            Log.Info(
                $"被取消, HasExited={process.HasExited}, PID={process.Id}");
            var killSw = Stopwatch.StartNew();
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            killSw.Stop();
            Log.Info(
                $"Kill 进程完成, 耗时 {killSw.ElapsedMilliseconds}ms, PID={process.Id}");
            throw;
        }
        finally
        {
            Log.Info( $"进入 finally, PID={process.Id}");
            var cleanSw = Stopwatch.StartNew();
            try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }
            cleanSw.Stop();
            Log.Info(
                $"finally 完成, 清理 tmp 耗时 {cleanSw.ElapsedMilliseconds}ms, PID={process.Id}");
        }
    }

    public double GetVideoDuration(string videoPath)
    {
        try
        {
            string ffprobePath = _ffmpegPath.Replace("ffmpeg.exe", "ffprobe.exe");
            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{videoPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return 0;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            if (double.TryParse(output.Trim(), out var sec))
                return sec;
        }
        catch (Exception ex)
        {
            Log.Info(
                $"ffprobe 失败: {ex.Message}");
        }
        return 0;
    }
}
