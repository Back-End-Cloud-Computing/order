using System.Text.Json;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Infrastructure.Messaging;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class OrderConfirmedSerializerTests
{
    [Fact]
    public void Serialize_ShouldMatchPromotionVersionOneContractWithoutPersonalOrPriceData()
    {
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

        var json = OrderConfirmedSerializer.Serialize(new OrderConfirmedEvent(
            eventId,
            occurredAt,
            orderId,
            null,
            [new OrderConfirmedItem("product-abc", 2)],
            "correlation-123"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal(eventId, root.GetProperty("event_id").GetGuid());
        Assert.Equal("OrderConfirmed", root.GetProperty("event_type").GetString());
        Assert.Equal(occurredAt, root.GetProperty("occurred_at").GetDateTimeOffset());
        Assert.Equal(orderId, root.GetProperty("order_id").GetGuid());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("coupon_code").ValueKind);
        Assert.Equal("product-abc", root.GetProperty("items")[0].GetProperty("product_id").GetString());
        Assert.Equal(2, root.GetProperty("items")[0].GetProperty("quantity").GetInt32());
        Assert.Equal("correlation-123", root.GetProperty("correlation_id").GetString());
        Assert.False(root.TryGetProperty("customer_email", out _));
        Assert.False(root.TryGetProperty("price", out _));
    }
}
