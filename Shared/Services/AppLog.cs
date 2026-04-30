using System;
using System.IO;

namespace LocalPlayer.Shared.Services;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class AppLog
{
    private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly object Lock = new();

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public static void Write(string fileName, string category, LogLevel level, string message)
    {
        if (level < MinimumLevel) return;

        try
        {
            string path = Path.Combine(BaseDir, fileName);
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.ToString().ToUpperInvariant(),-5}] [{category}] {message}{Environment.NewLine}";
            lock (Lock)
            {
                File.AppendAllText(path, line);
            }
        }
        catch { }
    }

    private const string DefaultLogFile = "player.log";

    public static void Debug(string category, string message)
        => Write(DefaultLogFile, category, LogLevel.Debug, message);

    public static void Info(string category, string message)
        => Write(DefaultLogFile, category, LogLevel.Info, message);

    public static void Warning(string category, string message)
        => Write(DefaultLogFile, category, LogLevel.Warning, message);

    public static void Error(string category, string message)
        => Write(DefaultLogFile, category, LogLevel.Error, message);

    public static void Error(string category, string message, Exception? ex)
    {
        string detail = ex != null ? $" | {ex.GetType().Name}: {ex.Message}" : "";
        Write(DefaultLogFile, category, LogLevel.Error, $"{message}{detail}");
    }
}
