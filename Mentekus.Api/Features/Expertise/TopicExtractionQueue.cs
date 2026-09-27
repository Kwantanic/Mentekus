using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Mentekus.Api.Features.Expertise;

internal sealed class TopicExtractionQueue(ILogger<TopicExtractionQueue> logger) : ITopicExtractionQueue
{
    private const int Capacity = 256;
    private readonly Channel<TopicExtractionJob> _channel = Channel.CreateBounded<TopicExtractionJob>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    private int _pending;

    public ValueTask EnqueueAsync(Guid userId, string text, string sourceType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ValueTask.CompletedTask;

        Interlocked.Increment(ref _pending);
        if (_channel.Writer.TryWrite(new TopicExtractionJob(userId, text, sourceType)))
            return ValueTask.CompletedTask;

        Interlocked.Decrement(ref _pending);
        logger.LogWarning("Topic extraction queue is full. Dropping topics for user {UserId}.", userId);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<TopicExtractionJob> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return job;
    }

    public void MarkCompleted() => Interlocked.Decrement(ref _pending);

    public async Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        while (Volatile.Read(ref _pending) > 0)
            await Task.Delay(10, cancellationToken);
    }
}
