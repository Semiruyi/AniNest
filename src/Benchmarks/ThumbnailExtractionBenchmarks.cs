using System.Diagnostics;
using BenchmarkDotNet.Attributes;

namespace AniNest.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class ThumbnailExtractionBenchmarks
{
    private string _videoPath = string.Empty;
    private string _ffmpegPath = string.Empty;
    private string _workspaceDir = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _videoPath = Environment.GetEnvironmentVariable("ANINEST_BENCHMARK_VIDEO") ?? ResolveDefaultVideoPath();
        if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
            throw new InvalidOperationException(
                "Set ANINEST_BENCHMARK_VIDEO to a real local video file before running benchmarks, or add the default sample clip.");

        _ffmpegPath = Environment.GetEnvironmentVariable("ANINEST_BENCHMARK_FFMPEG") ?? ResolveFfmpegPath();
        if (!File.Exists(_ffmpegPath))
            throw new InvalidOperationException($"ffmpeg not found: {_ffmpegPath}");

        _workspaceDir = Path.Combine(Path.GetTempPath(), $"AniNestBench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspaceDir);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_workspaceDir))
                Directory.Delete(_workspaceDir, recursive: true);
        }
        catch
        {
        }
    }

    [Benchmark(Baseline = true)]
    public int FullDecode_Fps1()
    {
        string outputDir = PrepareOutputDirectory("full");
        string arguments =
            $"-hide_banner -loglevel error -y -i \"{_videoPath}\" " +
            "-vf \"fps=1,scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease\" " +
            $"-q:v 5 \"{Path.Combine(outputDir, "%04d.jpg")}\"";

        RunFfmpeg(arguments);
        return Directory.GetFiles(outputDir, "*.jpg").Length;
    }

    [Benchmark]
    public int KeyframeOnly_Scaled()
    {
        string outputDir = PrepareOutputDirectory("keyframes");
        string arguments =
            $"-hide_banner -loglevel error -skip_frame nokey -y -i \"{_videoPath}\" " +
            "-vf \"scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease\" " +
            $"-vsync vfr -q:v 5 \"{Path.Combine(outputDir, "%04d.jpg")}\"";

        RunFfmpeg(arguments);
        return Directory.GetFiles(outputDir, "*.jpg").Length;
    }

    private static string ResolveFfmpegPath()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, "tools", "ffmpeg", "ffmpeg.exe");
            if (File.Exists(candidate))
                return candidate;

            DirectoryInfo? parent = Directory.GetParent(directory);
            directory = parent?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
    }

    private static string ResolveDefaultVideoPath()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, "src", "Benchmarks", "Samples", "bocchi-01-sample.mp4");
            if (File.Exists(candidate))
                return candidate;

            DirectoryInfo? parent = Directory.GetParent(directory);
            directory = parent?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "Samples", "bocchi-01-sample.mp4");
    }

    private string PrepareOutputDirectory(string name)
    {
        string path = Path.Combine(_workspaceDir, name);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        Directory.CreateDirectory(path);
        return path;
    }

    private void RunFfmpeg(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start ffmpeg.");

        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with {process.ExitCode}: {stderr}");
    }
}
