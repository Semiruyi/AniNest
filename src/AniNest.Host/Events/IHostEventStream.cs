using AniNest.Contracts.Events;

namespace AniNest.Host.Events;

internal interface IHostEventStream
{
    void Publish(string type, object payload);

    IAsyncEnumerable<EventEnvelopeDto> Subscribe(CancellationToken cancellationToken = default);
}
