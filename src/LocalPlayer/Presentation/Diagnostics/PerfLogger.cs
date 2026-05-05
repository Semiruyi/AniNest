using System;
using System.IO;
using System.Text.Json;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;

namespace LocalPlayer.Presentation.Diagnostics;

public static class PerfLogger
{
    private static readonly object Lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool Enabled { get; set; }

    public static string LogPath { get; set; } =
        AppPaths.PerfLogPath;

    static PerfLogger()
    {
#if DEBUG
        Enabled = true;
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
        if (!Enabled) return;
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
        if (!Enabled) return;
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




