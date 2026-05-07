using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LocalPlayer.Infrastructure.Paths;

namespace LocalPlayer.Infrastructure.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class AppLog
{
    private readonly record struct LogEntry(
        string FileName,
        string Category,
        LogLevel Level,
        DateTime TimestampLocal,
        string Message);

    private const string DefaultLogFile = "player.log";
    private const int MaxQueueCapacity = 8192;
    private const int BatchSize = 128;
    private static readonly Channel<LogEntry> Queue = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(MaxQueueCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropWrite,
        AllowSynchronousContinuations = false
    });
    private static readonly CancellationTokenSource ShutdownCts = new();
    private static readonly Task WorkerTask;
    private static long _droppedDebugCount;
    private static long _droppedInfoCount;
    private static long _droppedWarningCount;
    private static long _droppedErrorCount;

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    static AppLog()
    {
#if DEBUG
        try { File.Delete(AppPaths.PlayerLogPath); } catch { }
#else
        MinimumLevel = LogLevel.Info;
#endif
        WorkerTask = Task.Run(ProcessQueueAsync);
    }

    public static void Write(string fileName, string category, LogLevel level, string message)
    {
        if (level < MinimumLevel) return;

        var entry = new LogEntry(
            fileName,
            category,
            level,
            DateTime.Now,
            message);

        if (!Queue.Writer.TryWrite(entry))
        {
            RecordDropped(level);
        }
    }

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

    public static void Shutdown(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(1);
        try
        {
            Queue.Writer.TryComplete();
            ShutdownCts.CancelAfter(effectiveTimeout);
            WorkerTask.Wait(effectiveTimeout);
        }
        catch
        {
        }
    }

    private static void RecordDropped(LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Debug:
                Interlocked.Increment(ref _droppedDebugCount);
                break;
            case LogLevel.Info:
                Interlocked.Increment(ref _droppedInfoCount);
                break;
            case LogLevel.Warning:
                Interlocked.Increment(ref _droppedWarningCount);
                break;
            case LogLevel.Error:
                Interlocked.Increment(ref _droppedErrorCount);
                break;
        }
    }

    private static async Task ProcessQueueAsync()
    {
        var writers = new Dictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);
        var batch = new List<LogEntry>(BatchSize);

        try
        {
            while (await Queue.Reader.WaitToReadAsync(ShutdownCts.Token))
            {
                batch.Clear();

                while (batch.Count < BatchSize && Queue.Reader.TryRead(out var entry))
                    batch.Add(entry);

                if (batch.Count == 0)
                    continue;

                WriteDroppedSummaryIfNeeded(batch);

                foreach (var entry in batch)
                {
                    var writer = GetOrCreateWriter(writers, entry.FileName);
                    writer.Write(FormatLine(entry));
                }

                foreach (var writer in writers.Values)
                    writer.Flush();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            DrainRemainingEntries(batch);
            WriteDroppedSummaryIfNeeded(batch);

            foreach (var entry in batch)
            {
                try
                {
                    var writer = GetOrCreateWriter(writers, entry.FileName);
                    writer.Write(FormatLine(entry));
                }
                catch
                {
                }
            }

            foreach (var writer in writers.Values)
            {
                try
                {
                    writer.Flush();
                    writer.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    private static void DrainRemainingEntries(List<LogEntry> batch)
    {
        while (Queue.Reader.TryRead(out var entry))
            batch.Add(entry);
    }

    private static void WriteDroppedSummaryIfNeeded(List<LogEntry> batch)
    {
        var droppedDebug = Interlocked.Exchange(ref _droppedDebugCount, 0);
        var droppedInfo = Interlocked.Exchange(ref _droppedInfoCount, 0);
        var droppedWarning = Interlocked.Exchange(ref _droppedWarningCount, 0);
        var droppedError = Interlocked.Exchange(ref _droppedErrorCount, 0);

        if (droppedDebug == 0 && droppedInfo == 0 && droppedWarning == 0 && droppedError == 0)
            return;

        var builder = new StringBuilder("Dropped logs due to backpressure:");
        if (droppedDebug > 0) builder.Append($" debug={droppedDebug}");
        if (droppedInfo > 0) builder.Append($" info={droppedInfo}");
        if (droppedWarning > 0) builder.Append($" warning={droppedWarning}");
        if (droppedError > 0) builder.Append($" error={droppedError}");

        batch.Add(new LogEntry(
            DefaultLogFile,
            "Logging",
            LogLevel.Warning,
            DateTime.Now,
            builder.ToString()));
    }

    private static StreamWriter GetOrCreateWriter(IDictionary<string, StreamWriter> writers, string fileName)
    {
        if (writers.TryGetValue(fileName, out var existing))
            return existing;

        string path = AppPaths.ResolveInLogs(fileName);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 64 * 1024);
        var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 16 * 1024)
        {
            AutoFlush = false
        };
        writers[fileName] = writer;
        return writer;
    }

    private static string FormatLine(LogEntry entry)
        => $"[{entry.TimestampLocal:HH:mm:ss.fff}] [{entry.Level.ToString().ToUpperInvariant(),-5}] [{entry.Category}] {entry.Message}{Environment.NewLine}";
}

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



