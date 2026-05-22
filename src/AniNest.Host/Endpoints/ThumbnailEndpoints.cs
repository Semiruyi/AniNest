using AniNest.Application.Modules;

namespace AniNest.Host.Endpoints;

internal static class ThumbnailEndpoints
{
    public static IEndpointRouteBuilder MapThumbnailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/thumbnails").WithTags("Thumbnails");

        group.MapGet("/folders/{folderId}", (string folderId, IThumbnailModule module, CancellationToken cancellationToken)
            => module.GetByFolderAsync(folderId, cancellationToken));

        group.MapGet("/videos/{videoId}", async (string videoId, IThumbnailModule module, CancellationToken cancellationToken) =>
        {
            var status = await module.GetByVideoAsync(videoId, cancellationToken);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        group.MapPost("/folders/{folderId}:prioritize", async (string folderId, IThumbnailModule module, CancellationToken cancellationToken) =>
        {
            await module.PrioritizeFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/folders/{folderId}:regenerate", async (string folderId, IThumbnailModule module, CancellationToken cancellationToken) =>
        {
            await module.RegenerateFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        group.MapDelete("/folders/{folderId}/cache", async (string folderId, IThumbnailModule module, CancellationToken cancellationToken) =>
        {
            await module.ClearFolderCacheAsync(folderId, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
