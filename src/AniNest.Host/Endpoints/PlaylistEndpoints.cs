using AniNest.Application.Modules;

namespace AniNest.Host.Endpoints;

internal static class PlaylistEndpoints
{
    public static IEndpointRouteBuilder MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/playlist").WithTags("Playlist");

        group.MapGet("/current", async (IPlaylistModule module, CancellationToken cancellationToken) =>
        {
            var playlist = await module.GetCurrentAsync(cancellationToken);
            return playlist is null ? Results.NoContent() : Results.Ok(playlist);
        });

        group.MapGet("/by-folder/{folderId}", (string folderId, IPlaylistModule module, CancellationToken cancellationToken)
            => module.GetByFolderAsync(folderId, cancellationToken));

        group.MapPost("/by-folder/{folderId}:activate", (string folderId, IPlaylistModule module, CancellationToken cancellationToken)
            => module.ActivateFolderAsync(folderId, cancellationToken));

        group.MapPost("/current/items/{itemId}:select", (string itemId, IPlaylistModule module, CancellationToken cancellationToken)
            => module.SelectItemAsync(itemId, cancellationToken));

        group.MapPost("/current:next", (IPlaylistModule module, CancellationToken cancellationToken)
            => module.MoveNextAsync(cancellationToken));

        group.MapPost("/current:previous", (IPlaylistModule module, CancellationToken cancellationToken)
            => module.MovePreviousAsync(cancellationToken));

        return app;
    }
}
