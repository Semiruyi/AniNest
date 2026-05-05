using System;
using System.IO;
using System.Text.Json;

namespace LocalPlayer.View.Diagnostics;

public static class PerfLogger
{
    private static readonly object Lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string LogPath { get; set; } =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf.log");

    static PerfLogger()
    {
#if DEBUG
        try
        {
            if (File.Exists(LogPath))
                File.Delete(LogPath);
        }
        catch
        {
        }
#endif
    }

    public static void Write(PerfSceneReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        string line = JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine;
        string? directory = Path.GetDirectoryName(LogPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        lock (Lock)
        {
            File.AppendAllText(LogPath, line);
        }
    }

    public static void Write(PerfSpanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        string line = JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine;
        string? directory = Path.GetDirectoryName(LogPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        lock (Lock)
        {
            File.AppendAllText(LogPath, line);
        }
    }
}
