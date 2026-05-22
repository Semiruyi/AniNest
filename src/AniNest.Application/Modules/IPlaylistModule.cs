using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;

namespace AniNest.Application.Modules;

public interface IPlaylistModule
{
    Task<PlaylistDto> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task<PlaylistDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> ActivateFolderAsync(string folderId, CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> SelectItemAsync(string itemId, CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> MoveNextAsync(CancellationToken cancellationToken = default);
    Task<SessionOpenResultDto> MovePreviousAsync(CancellationToken cancellationToken = default);
}
