using AniNest.Application.Modules;
using AniNest.Contracts.Session;

namespace AniNest.Host.Endpoints;

internal static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/session").WithTags("Session");

        group.MapGet("/", async (ISessionModule module, CancellationToken cancellationToken) =>
        {
            var session = await module.GetCurrentAsync(cancellationToken);
            return session is null ? Results.NoContent() : Results.Ok(session);
        });

        group.MapPost("/open-folder", (SessionOpenFolderRequest request, ISessionModule module, CancellationToken cancellationToken)
            => module.OpenFolderAsync(request, cancellationToken));

        group.MapPost("/select-item", (SessionSelectItemRequest request, ISessionModule module, CancellationToken cancellationToken)
            => module.SelectItemAsync(request, cancellationToken));

        group.MapPost("/next", (ISessionModule module, CancellationToken cancellationToken)
            => module.MoveNextAsync(cancellationToken));

        group.MapPost("/previous", (ISessionModule module, CancellationToken cancellationToken)
            => module.MovePreviousAsync(cancellationToken));

        group.MapPost("/progress", async (SessionProgressReportRequest request, ISessionModule module, CancellationToken cancellationToken) =>
        {
            await module.ReportProgressAsync(request, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/complete", async (SessionCompleteRequest request, ISessionModule module, CancellationToken cancellationToken) =>
        {
            await module.CompleteAsync(request, cancellationToken);
            return Results.Accepted();
        });

        group.MapPost("/close", async (ISessionModule module, CancellationToken cancellationToken) =>
        {
            await module.CloseAsync(cancellationToken);
            return Results.Accepted();
        });

        return app;
    }
}
