namespace AniNest.Application.Resources;

public sealed record ResourceKey(
    ResourceKind Kind,
    string OwnerId);
