using System.Text.Json;
using AniNest.Contracts.Common;

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
            catch (Exception ex)
            {
                await WriteErrorResponseAsync(context, ex);
            }
        });
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var (statusCode, payload) = MapException(exception);
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
