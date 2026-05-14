using System.Threading.Channels;
using AniNest.Infrastructure.Logging;

namespace AniNest.Features.Metadata;

public sealed class MetadataTaskStore
{
    private static readonly Logger Log = AppLog.For<MetadataTaskStore>();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public bool Enqueue(string folderPath)
    {
        lock (_sync)
        {
            if (!_pendingPaths.Add(folderPath))
            {
                Log.Debug($"Enqueue skipped: instance={GetHashCode()} path={folderPath}");
                return false;
            }
        }

        if (_queue.Writer.TryWrite(folderPath))
        {
            Log.Info($"Enqueue success: instance={GetHashCode()} path={folderPath}");
            return true;
        }

        lock (_sync)
        {
            _pendingPaths.Remove(folderPath);
        }

        Log.Warning($"Enqueue failed: instance={GetHashCode()} path={folderPath}");
        return false;
    }

    public async ValueTask<string> DequeueAsync(CancellationToken ct)
    {
        string folderPath = await _queue.Reader.ReadAsync(ct);
        lock (_sync)
        {
            _pendingPaths.Remove(folderPath);
        }

        Log.Info($"Dequeue success: instance={GetHashCode()} path={folderPath}");
        return folderPath;
    }
}
