using Ganjj.Order.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Infrastructure.Messaging;

internal sealed class OutboxProcessor(
    IOutboxStore store,
    IOrderConfirmedPublisher publisher,
    TimeProvider timeProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxProcessor> logger)
{
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var messages = await store.ClaimAsync(
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(settings.LeaseSeconds),
            settings.BatchSize,
            cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                await store.MarkPublishedAsync(message, timeProvider.GetUtcNow(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8)));
                await store.MarkFailedAsync(
                    message,
                    timeProvider.GetUtcNow().AddSeconds(delaySeconds),
                    exception.Message,
                    CancellationToken.None);
                logger.LogWarning(
                    exception,
                    "OrderConfirmed event {EventId} could not be published on attempt {AttemptCount}.",
                    message.Id,
                    message.AttemptCount);
            }
        }

        return messages.Count;
    }
}
