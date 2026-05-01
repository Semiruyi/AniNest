using System;
using System.IO;

namespace LocalPlayer.Model;

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

    static AppLog()
    {
#if DEBUG
        try { File.Delete(Path.Combine(BaseDir, "player.log")); } catch { }
#else
        MinimumLevel = LogLevel.Info;
#endif
    }

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

    public static Logger For<T>() => new(typeof(T).Name);
    public static Logger For(string category) => new(category);
}

/// <summary>零分配、零 ceremony 的日志入口。用法：Log.Info("message"); Log.Warning("..."); Log.Error("...", ex);</summary>
public readonly struct Logger
{
    private readonly string _category;

    internal Logger(string category) => _category = category;

    public void Debug(string message) => AppLog.Debug(_category, message);
    public void Info(string message) => AppLog.Info(_category, message);
    public void Warning(string message) => AppLog.Warning(_category, message);
    public void Error(string message) => AppLog.Error(_category, message);
    public void Error(string message, Exception? ex) => AppLog.Error(_category, message, ex);
}
