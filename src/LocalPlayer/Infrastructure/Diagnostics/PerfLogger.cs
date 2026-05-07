using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;

namespace AniNest.Infrastructure.Diagnostics;

public static class PerfLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private const int MaxQueueCapacity = 4096;
    private const int BatchSize = 128;
    private static readonly Channel<string> Queue = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxQueueCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropWrite,
        AllowSynchronousContinuations = false
    });
    private static readonly CancellationTokenSource ShutdownCts = new();
    private static readonly Task WorkerTask;
    private static long _droppedCount;

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
        WorkerTask = Task.Run(ProcessQueueAsync);
    }

    public static void Write(PerfSceneReport report)
    {
        if (!Enabled) return;
        ArgumentNullException.ThrowIfNull(report);
        Enqueue(JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine);
    }

    public static void Write(PerfSpanReport report)
    {
        if (!Enabled) return;
        ArgumentNullException.ThrowIfNull(report);
        Enqueue(JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine);
    }

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

    private static void Enqueue(string line)
    {
        if (!Queue.Writer.TryWrite(line))
            Interlocked.Increment(ref _droppedCount);
    }

    private static async Task ProcessQueueAsync()
    {
        StreamWriter? writer = null;
        var batch = new List<string>(BatchSize);

        try
        {
            while (await Queue.Reader.WaitToReadAsync(ShutdownCts.Token))
            {
                batch.Clear();

                while (batch.Count < BatchSize && Queue.Reader.TryRead(out var line))
                    batch.Add(line);

                if (batch.Count == 0)
                    continue;

                AppendDroppedSummary(batch);
                writer ??= CreateWriter();

                foreach (var line in batch)
                    writer.Write(line);

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
            AppendDroppedSummary(batch);

            if (batch.Count > 0)
            {
                writer ??= CreateWriter();
                foreach (var line in batch)
                    writer.Write(line);
            }

            if (writer != null)
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

    private static void DrainRemainingEntries(List<string> batch)
    {
        while (Queue.Reader.TryRead(out var line))
            batch.Add(line);
    }

    private static void AppendDroppedSummary(List<string> batch)
    {
        var dropped = Interlocked.Exchange(ref _droppedCount, 0);
        if (dropped <= 0)
            return;

        batch.Add(JsonSerializer.Serialize(new
        {
            type = "perf-log-drop",
            dropped,
            at = DateTimeOffset.UtcNow
        }, JsonOptions) + Environment.NewLine);
    }

    private static StreamWriter CreateWriter()
    {
        string? directory = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 64 * 1024);
        return new StreamWriter(stream, bufferSize: 16 * 1024)
        {
            AutoFlush = false
        };
    }
}
