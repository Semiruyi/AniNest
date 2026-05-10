using AniNest.Infrastructure.Persistence;

namespace AniNest.Features.Library.Models;

public sealed record FolderStatusChangeRequest(
    FolderListItem Item,
    WatchStatus Status);
