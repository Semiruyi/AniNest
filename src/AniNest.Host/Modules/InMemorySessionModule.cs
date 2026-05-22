using AniNest.Application.Modules;
using AniNest.Contracts.Session;

namespace AniNest.Host.Modules;

internal sealed class InMemorySessionModule : ISessionModule
{
    private SessionStateDto? _current = new(
        "session-sample",
        "sample-folder",
        "Sample Anime",
        "ep-01",
        0,
        12,
        false,
        true,
        0,
        1.0,
        80);

    public Task<SessionStateDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_current);

    public Task<SessionOpenResultDto> OpenFolderAsync(SessionOpenFolderRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(CreateResult(request.FolderId, "ep-01", 0));

    public Task<SessionOpenResultDto> SelectItemAsync(SessionSelectItemRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(CreateResult("sample-folder", request.ItemId, 0));

    public Task<SessionOpenResultDto> MoveNextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateResult("sample-folder", "ep-02", 1));

    public Task<SessionOpenResultDto> MovePreviousAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateResult("sample-folder", "ep-01", 0));

    public Task ReportProgressAsync(SessionProgressReportRequest request, CancellationToken cancellationToken = default)
    {
        if (_current is not null)
        {
            _current = _current with { SavedProgressMs = request.PositionMs };
        }

        return Task.CompletedTask;
    }

    public Task CompleteAsync(SessionCompleteRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _current = null;
        return Task.CompletedTask;
    }

    private SessionOpenResultDto CreateResult(string folderId, string itemId, int index)
    {
        _current = new SessionStateDto(
            "session-sample",
            folderId,
            "Sample Anime",
            itemId,
            index,
            12,
            index > 0,
            index < 11,
            0,
            1.0,
            80);

        return new SessionOpenResultDto(
            _current,
            new PlaybackTargetDto(itemId, $"Episode {index + 1}", $"D:/Media/Sample Anime/{index + 1:00}.mp4", 0));
    }
}
