using AniNest.Contracts.Session;

namespace AniNest.Application.Playback;

public sealed record FolderActivationResult(
    SessionOpenResultDto Result,
    bool SessionChanged);
