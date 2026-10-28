namespace Ganjj.Order.Infrastructure.Messaging;

internal sealed record OutboxEnvelope(
    Guid Id,
    string EventType,
    string Payload,
    string? CorrelationId,
    int AttemptCount,
    Guid LockId);

internal interface IOutboxStore
{
    Task<IReadOnlyList<OutboxEnvelope>> ClaimAsync(
        DateTimeOffset now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken);

    Task<bool> RenewLeaseAsync(
        OutboxEnvelope message,
        DateTimeOffset now,
        TimeSpan lease,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(OutboxEnvelope message, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkFailedAsync(
        OutboxEnvelope message,
        DateTimeOffset nextAttemptAt,
        string error,
        CancellationToken cancellationToken);
}

internal interface IOrderConfirmedPublisher
{
    Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken);
}
