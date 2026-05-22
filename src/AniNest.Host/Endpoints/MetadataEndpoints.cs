using AniNest.Application.Modules;
using AniNest.Contracts.Metadata;

namespace AniNest.Host.Endpoints;

internal static class MetadataEndpoints
{
    public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metadata").WithTags("Metadata");

        group.MapGet("/status-summary", (IMetadataModule module, CancellationToken cancellationToken)
            => module.GetSummaryAsync(cancellationToken));

        group.MapGet("/folders/{folderId}", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            var metadata = await module.GetByFolderAsync(folderId, cancellationToken);
            return metadata is null ? Results.NotFound() : Results.Ok(metadata);
        });

        group.MapPost("/folders/{folderId}:refresh", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RefreshFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost(":retry-failed", async (RetryFailedMetadataRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RetryFailedAsync(request.IncludeNoMatch, cancellationToken);
            return Results.Accepted();
        });

        return app;
    }
}
