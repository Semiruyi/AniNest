namespace AniNest.Host.Endpoints;

internal static class PlaylistEndpoints
{
    public static IEndpointRouteBuilder MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/playlist").WithTags("Playlist");

        group.MapGet("/current", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        group.MapGet("/by-folder/{folderId}", () => Results.StatusCode(StatusCodes.Status501NotImplemented));

        return app;
    }
}
