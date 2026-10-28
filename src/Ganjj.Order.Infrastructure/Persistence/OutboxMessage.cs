namespace Ganjj.Order.Infrastructure.Persistence;

internal sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public string? CorrelationId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LockId { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LastError { get; set; }
}
