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
            await module.AddFolderAsync(request, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/folders:batch-add", async (BatchAddLibraryFoldersRequest request, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.AddFolderBatchAsync(request, cancellationToken);
            return Results.Accepted();
        });

        group.MapDelete("/folders/{folderId}", async (string folderId, ILibraryModule module, CancellationToken cancellationToken) =>
        {
            await module.DeleteFolderAsync(folderId, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
