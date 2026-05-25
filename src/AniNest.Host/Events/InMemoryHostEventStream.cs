using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AniNest.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Events;

internal sealed class InMemoryHostEventStream : IHostEventStream
{
    private readonly ConcurrentDictionary<Guid, Channel<EventEnvelopeDto>> _subscribers = new();
    private long _nextSequence;
    private readonly ILogger<InMemoryHostEventStream> _logger;

    public InMemoryHostEventStream(ILogger<InMemoryHostEventStream> logger)
    {
        _logger = logger;
    }

    public void Publish(string type, object payload)
    {
        var sequence = Interlocked.Increment(ref _nextSequence);
        var envelope = new EventEnvelopeDto(type, DateTimeOffset.UtcNow, sequence, payload);
        _logger.LogInformation(
            "Host event published. Type={Type}, Sequence={Sequence}, SubscriberCount={SubscriberCount}, PayloadType={PayloadType}",
            type,
            sequence,
            _subscribers.Count,
            payload.GetType().Name);
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
        _logger.LogInformation(
            "Host event subscriber connected. SubscriberId={SubscriberId}, SubscriberCount={SubscriberCount}",
            subscriberId,
            _subscribers.Count);

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
            _logger.LogInformation(
                "Host event subscriber disconnected. SubscriberId={SubscriberId}, SubscriberCount={SubscriberCount}",
                subscriberId,
                _subscribers.Count);
            channel.Writer.TryComplete();
        }
    }
}
