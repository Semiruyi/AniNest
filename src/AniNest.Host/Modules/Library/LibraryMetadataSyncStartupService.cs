using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class LibraryMetadataSyncStartupService : IHostedService
{
    private readonly LibraryMetadataSyncService _sync;
    private readonly ILogger<LibraryMetadataSyncStartupService> _logger;

    public LibraryMetadataSyncStartupService(
        LibraryMetadataSyncService sync,
        ILogger<LibraryMetadataSyncStartupService> logger)
    {
        _sync = sync;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Library metadata startup sync starting.");
        await _sync.SyncAsync(cancellationToken);
        _logger.LogInformation("Library metadata startup sync completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
