using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AniNest.Contracts.Events;

namespace AniNest.Host.Events;

internal sealed class InMemoryHostEventStream : IHostEventStream
{
    private readonly ConcurrentDictionary<Guid, Channel<EventEnvelopeDto>> _subscribers = new();

    public void Publish(string type, object payload)
    {
        var envelope = new EventEnvelopeDto(type, DateTimeOffset.UtcNow, payload);
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(envelope);
        }
    }

    public async IAsyncEnumerable<EventEnvelopeDto> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<EventEnvelopeDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[subscriberId] = channel;

        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return envelope;
            }
        }
        finally
        {
            _subscribers.TryRemove(subscriberId, out _);
            channel.Writer.TryComplete();
        }
    }
}
