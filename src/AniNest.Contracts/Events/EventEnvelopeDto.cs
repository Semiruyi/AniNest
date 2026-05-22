namespace AniNest.Contracts.Events;

public sealed record EventEnvelopeDto(
    string Type,
    DateTimeOffset TimestampUtc,
    object Payload);
