using Ganjj.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ganjj.Order.Infrastructure.Messaging;

internal sealed class OutboxStore(OrderDbContext dbContext) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxEnvelope>> ClaimAsync(
        DateTimeOffset now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var candidateIds = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.PublishedAt == null &&
                              message.NextAttemptAt <= now &&
                              (message.LockedUntil == null || message.LockedUntil < now))
            .OrderBy(message => message.OccurredAt)
            .Select(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var claimed = new List<OutboxEnvelope>(candidateIds.Count);
        foreach (var candidateId in candidateIds)
        {
            var lockId = Guid.NewGuid();
            var updated = await dbContext.OutboxMessages
                .Where(message => message.Id == candidateId &&
                                  message.PublishedAt == null &&
                                  message.NextAttemptAt <= now &&
                                  (message.LockedUntil == null || message.LockedUntil < now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.LockId, lockId)
                    .SetProperty(message => message.LockedUntil, now.Add(lease))
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1),
                    cancellationToken);

            if (updated == 0) continue;

            var message = await dbContext.OutboxMessages.AsNoTracking()
                .SingleAsync(value => value.Id == candidateId && value.LockId == lockId, cancellationToken);
            claimed.Add(new OutboxEnvelope(
                message.Id,
                message.EventType,
                message.Payload,
                message.CorrelationId,
                message.AttemptCount,
                lockId));
        }

        return claimed;
    }

    public async Task<bool> RenewLeaseAsync(
        OutboxEnvelope message,
        DateTimeOffset now,
        TimeSpan lease,
        CancellationToken cancellationToken)
    {
        var updated = await dbContext.OutboxMessages
            .Where(value => value.Id == message.Id &&
                            value.LockId == message.LockId &&
                            value.PublishedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.LockedUntil, now.Add(lease)), cancellationToken);
        return updated == 1;
    }

    public Task MarkPublishedAsync(
        OutboxEnvelope message,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.OutboxMessages
            .Where(value => value.Id == message.Id && value.LockId == message.LockId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.PublishedAt, now)
                .SetProperty(value => value.LockId, (Guid?)null)
                .SetProperty(value => value.LockedUntil, (DateTimeOffset?)null)
                .SetProperty(value => value.LastError, (string?)null), cancellationToken);

    public Task MarkFailedAsync(
        OutboxEnvelope message,
        DateTimeOffset nextAttemptAt,
        string error,
        CancellationToken cancellationToken) =>
        dbContext.OutboxMessages
            .Where(value => value.Id == message.Id && value.LockId == message.LockId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.NextAttemptAt, nextAttemptAt)
                .SetProperty(value => value.LockId, (Guid?)null)
                .SetProperty(value => value.LockedUntil, (DateTimeOffset?)null)
                .SetProperty(value => value.LastError, error[..Math.Min(error.Length, 2000)]), cancellationToken);
}
