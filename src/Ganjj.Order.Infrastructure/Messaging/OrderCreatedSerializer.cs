using System.Text.Json;
using System.Text.Json.Serialization;
using Ganjj.Order.Application.Abstractions;

namespace Ganjj.Order.Infrastructure.Messaging;

internal static class OrderCreatedSerializer
{
    public static string Serialize(OrderCreatedNotification notification) => JsonSerializer.Serialize(
        new SupportOrderCreatedRequest(
            notification.EventId,
            "OrderCreated",
            notification.OccurredAt,
            notification.OrderId.ToString("D"),
            notification.OrderCode,
            notification.CustomerId.ToString("D"),
            notification.CustomerEmail,
            notification.CustomerPhone,
            notification.Total,
            notification.Currency,
            notification.CorrelationId));

    private sealed record SupportOrderCreatedRequest(
        [property: JsonPropertyName("event_id")] Guid EventId,
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt,
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("order_code")] string OrderCode,
        [property: JsonPropertyName("customer_id")] string CustomerId,
        [property: JsonPropertyName("customer_email")] string CustomerEmail,
        [property: JsonPropertyName("customer_phone"),
         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CustomerPhone,
        [property: JsonPropertyName("total")] decimal Total,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("correlation_id"),
         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CorrelationId);
}
