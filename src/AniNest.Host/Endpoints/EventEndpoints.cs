using AniNest.Contracts.Events;
using AniNest.Host.Events;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Endpoints;

internal static class EventEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (HttpContext context, IHostEventStream stream, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("EventEndpoints");
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Append("X-Accel-Buffering", "no");
            context.Response.ContentType = "text/event-stream";
            logger.LogInformation(
                "SSE connection opened. TraceIdentifier={TraceIdentifier}, RemoteIp={RemoteIp}",
                context.TraceIdentifier,
                context.Connection.RemoteIpAddress?.ToString() ?? "(unknown)");

            await WriteEventAsync(
                context,
                new EventEnvelopeDto(
                    "host.connected",
                    DateTimeOffset.UtcNow,
                    0,
                    new { message = "AniNest host event stream connected." }),
                logger,
                cancellationToken);

            await foreach (var envelope in stream.Subscribe(cancellationToken))
            {
                await WriteEventAsync(context, envelope, logger, cancellationToken);
            }
        }).WithTags("Events");

        return app;
    }

    private static async Task WriteEventAsync(HttpContext context, EventEnvelopeDto envelope, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SSE event sent. TraceIdentifier={TraceIdentifier}, Type={Type}, Sequence={Sequence}",
            context.TraceIdentifier,
            envelope.Type,
            envelope.Sequence);
        await context.Response.WriteAsync($"id: {envelope.Sequence}\n", cancellationToken);
        await context.Response.WriteAsync($"event: {envelope.Type}\n", cancellationToken);
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(envelope, SerializerOptions)}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
}
