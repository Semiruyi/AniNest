using System.Text.Json;
using AniNest.Contracts.Common;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.ErrorHandling;

internal static class ApiExceptionHandlingExtensions
{
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ApiExceptionHandling");
                logger.LogInformation(
                    "Request aborted by client after response pipeline started. Path={Path}",
                    context.Request.Path);
            }
            catch (Exception ex)
            {
                await WriteErrorResponseAsync(context, ex);
            }
        });
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ApiExceptionHandling");

        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                exception,
                "Unhandled exception occurred after the response started. Path={Path}, Method={Method}",
                context.Request.Path,
                context.Request.Method);
            return;
        }

        var (statusCode, payload) = MapException(exception);
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await JsonSerializer.SerializeAsync(context.Response.Body, payload);
    }

    private static (int StatusCode, ErrorResponse Payload) MapException(Exception exception)
    {
        return exception switch
        {
            KeyNotFoundException ex => (
                StatusCodes.Status404NotFound,
                new ErrorResponse(
                    "resource.not_found",
                    ex.Message)),
            ArgumentException ex => (
                StatusCodes.Status400BadRequest,
                new ErrorResponse(
                    "request.invalid",
                    ex.Message)),
            InvalidOperationException ex => (
                StatusCodes.Status409Conflict,
                new ErrorResponse(
                    "request.conflict",
                    ex.Message)),
            _ => (
                StatusCodes.Status500InternalServerError,
                new ErrorResponse(
                    "server.unexpected_error",
                    "An unexpected server error occurred."))
        };
    }
}
