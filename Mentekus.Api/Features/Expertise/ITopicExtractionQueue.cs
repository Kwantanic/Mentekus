namespace Mentekus.Api.Features.Expertise;

public interface ITopicExtractionQueue
{
    // Best-effort. The contribution is already committed when this is called.
    ValueTask EnqueueAsync(Guid userId, string text, string sourceType, CancellationToken cancellationToken = default);
}

internal readonly record struct TopicExtractionJob(Guid UserId, string Text, string SourceType);
