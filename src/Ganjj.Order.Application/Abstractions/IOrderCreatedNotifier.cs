namespace Ganjj.Order.Application.Abstractions;

public sealed record OrderCreatedNotification(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderCode,
    Guid CustomerId,
    string CustomerEmail,
    string? CustomerPhone,
    decimal Total,
    string Currency,
    string? CorrelationId);

public interface IOrderCreatedNotifier
{
    Task NotifyAsync(
        OrderCreatedNotification notification,
        CancellationToken cancellationToken);
}
