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

        group.MapGet("/reviews", (IMetadataModule module, CancellationToken cancellationToken)
            => module.GetReviewQueueAsync(cancellationToken));

        group.MapGet("/reviews/{folderId}", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            var review = await module.GetReviewByFolderAsync(folderId, cancellationToken);
            return review is null ? Results.NotFound() : Results.Ok(review);
        });

        group.MapPost("/reviews/{folderId}:confirm", async (string folderId, ConfirmMetadataReviewRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.ConfirmReviewAsync(folderId, request.SourceId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/reviews/{folderId}:reject-candidate", async (string folderId, RejectMetadataReviewCandidateRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RejectReviewCandidateAsync(folderId, request.SourceId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/folders/{folderId}:refresh", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RefreshFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/folders/{folderId}:retry", async (string folderId, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RetryFolderAsync(folderId, cancellationToken);
            return Results.Accepted();
        });

        app.MapPost("/api/metadata:enqueue-missing", async (IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.EnqueueMissingAsync(cancellationToken);
            return Results.Accepted();
        }).WithTags("Metadata");

        app.MapPost("/api/metadata:retry-failed", async (RetryFailedMetadataRequest request, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            await module.RetryFailedAsync(request.IncludeNoMatch, cancellationToken);
            return Results.Accepted();
        }).WithTags("Metadata");

        app.MapPost("/api/metadata:process-queue", async (int? maxItems, IMetadataModule module, CancellationToken cancellationToken) =>
        {
            var payload = await module.ProcessQueueAsync(maxItems ?? 1, cancellationToken);
            return Results.Ok(payload);
        }).WithTags("Metadata");

        return app;
    }
}
