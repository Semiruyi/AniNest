using AniNest.Contracts.Events;

namespace AniNest.Host.Endpoints;

internal static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", () =>
        {
            var sample = new EventEnvelopeDto(
                "host.scaffold.ready",
                DateTimeOffset.UtcNow,
                new { message = "Event streaming scaffold is ready." });
            return Results.Ok(sample);
        }).WithTags("Events");

        return app;
    }
}
