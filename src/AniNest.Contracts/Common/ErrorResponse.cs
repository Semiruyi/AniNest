namespace AniNest.Contracts.Common;

public sealed record ErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Details = null);
