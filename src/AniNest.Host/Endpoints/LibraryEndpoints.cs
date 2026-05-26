using AniNest.Application.Modules;
using AniNest.Contracts.Library;

namespace AniNest.Host.Endpoints;

internal static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/library").WithTags("Library");

        group.MapGet("/folders", async (ILibraryModule module, CancellationToken cancellationToken) =>
        {
            var items = await module.GetFoldersAsync(cancellationToken);
            return Results.Ok(new LibraryFolderListResponse(items));
        });

        group.MapPost("/folders", async (AddLibraryFolderRequest request, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            var result = await module.AddFolderAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/folders:batch-add", async (BatchAddLibraryFoldersRequest request, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.AddFolderBatchAsync(request, cancellationToken);
            return Results.Accepted();
        });

        group.MapGet("/browser", async (string? path, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            var result = await module.BrowseAsync(path, cancellationToken);
            return Results.Ok(result);
        });

        group.MapDelete("/folders/{folderId}", async (string folderId, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.DeleteFolderAsync(folderId, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/folders/{folderId}:favorite", async (string folderId, SetFavoriteRequest request, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.SetFavoriteAsync(folderId, request.IsFavorite, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/folders/{folderId}:watch-status", async (string folderId, SetWatchStatusRequest request, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.SetWatchStatusAsync(folderId, request.Status, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/folders/{folderId}:move-to-front", async (string folderId, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.MoveFolderToFrontAsync(folderId, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
