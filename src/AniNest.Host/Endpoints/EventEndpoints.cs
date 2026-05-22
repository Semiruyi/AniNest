using AniNest.Contracts.Events;
using AniNest.Host.Events;
using System.Text.Json;

namespace AniNest.Host.Endpoints;

internal static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (HttpContext context, IHostEventStream stream, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Append("X-Accel-Buffering", "no");
            context.Response.ContentType = "text/event-stream";

            await WriteEventAsync(
                context,
                new EventEnvelopeDto(
                    "host.connected",
                    DateTimeOffset.UtcNow,
                    new { message = "AniNest host event stream connected." }),
                cancellationToken);

            await foreach (var envelope in stream.Subscribe(cancellationToken))
            {
                await WriteEventAsync(context, envelope, cancellationToken);
            }
        }).WithTags("Events");

        return app;
    }

    private static async Task WriteEventAsync(HttpContext context, EventEnvelopeDto envelope, CancellationToken cancellationToken)
    {
        await context.Response.WriteAsync($"event: {envelope.Type}\n", cancellationToken);
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(envelope)}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
}
