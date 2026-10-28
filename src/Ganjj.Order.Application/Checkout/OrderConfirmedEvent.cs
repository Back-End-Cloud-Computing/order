namespace Ganjj.Order.Application.Checkout;

public sealed record OrderConfirmedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string? CouponCode,
    IReadOnlyList<OrderConfirmedItem> Items,
    string? CorrelationId);

public sealed record OrderConfirmedItem(string ProductId, int Quantity);
