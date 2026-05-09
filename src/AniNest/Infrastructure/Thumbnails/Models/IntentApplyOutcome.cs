namespace AniNest.Infrastructure.Thumbnails;

internal enum IntentApplyOutcome
{
    Applied,
    AlreadyReady,
    HigherIntentAlreadyPresent,
    MissingTask
}
