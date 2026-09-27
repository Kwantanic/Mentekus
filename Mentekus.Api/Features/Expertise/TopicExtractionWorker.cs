namespace Mentekus.Api.Features.Expertise;

internal sealed class TopicExtractionWorker(
    TopicExtractionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<TopicExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expertise = scope.ServiceProvider.GetRequiredService<IExpertiseService>();
                await expertise.ApplyTopicsAsync(job.UserId, job.Text, job.SourceType, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Topic extraction failed for user {UserId} source {SourceType}.",
                    job.UserId, job.SourceType);
            }
            finally
            {
                queue.MarkCompleted();
            }
        }
    }
}
