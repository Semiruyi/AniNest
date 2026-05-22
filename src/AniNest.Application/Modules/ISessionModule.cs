using AniNest.Contracts.Session;

namespace AniNest.Application.Modules;

public interface ISessionModule
{
    Task<SessionStateDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> OpenFolderAsync(SessionOpenFolderRequest request, CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> SelectItemAsync(SessionSelectItemRequest request, CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> MoveNextAsync(CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> MovePreviousAsync(CancellationToken cancellationToken = default);
    Task ReportProgressAsync(SessionProgressReportRequest request, CancellationToken cancellationToken = default);
    Task CompleteAsync(SessionCompleteRequest request, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}
