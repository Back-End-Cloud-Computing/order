using Ganjj.Order.Domain.Orders;

namespace Ganjj.Order.Application.Checkout;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string BearerToken,
    string IdempotencyKey,
    DeliveryAddress DeliveryAddress,
    string? CorrelationId = null);

public sealed record CreateOrderResult(
    Guid Id,
    string Code,
    OrderStatus Status,
    int TrackingStep,
    decimal Total,
    DateTimeOffset CreatedAt)
{
    public static CreateOrderResult From(Ganjj.Order.Domain.Orders.Order order) =>
        new(order.Id, order.Code, order.Status, order.TrackingStep, order.Total, order.CreatedAt);
}
