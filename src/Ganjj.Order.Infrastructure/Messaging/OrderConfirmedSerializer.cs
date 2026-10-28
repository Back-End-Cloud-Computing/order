using System.Text.Json;
using System.Text.Json.Serialization;
using Ganjj.Order.Application.Checkout;

namespace Ganjj.Order.Infrastructure.Messaging;

internal static class OrderConfirmedSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize(OrderConfirmedEvent message) => JsonSerializer.Serialize(
        new Contract(
            1,
            message.EventId,
            "OrderConfirmed",
            message.OccurredAt,
            message.OrderId,
            message.CouponCode,
            message.Items.Select(item => new Item(item.ProductId, item.Quantity)).ToList(),
            message.CorrelationId),
        Options);

    private sealed record Contract(
        int SchemaVersion,
        Guid EventId,
        string EventType,
        DateTimeOffset OccurredAt,
        Guid OrderId,
        string? CouponCode,
        IReadOnlyList<Item> Items,
        string? CorrelationId);

    private sealed record Item(string ProductId, int Quantity);
}
