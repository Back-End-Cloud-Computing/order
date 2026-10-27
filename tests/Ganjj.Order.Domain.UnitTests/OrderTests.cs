using Ganjj.Order.Domain.Orders;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Domain.UnitTests;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_WhenItemsAreEmpty_ShouldRejectOrder()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            OrderAggregate.Create(Guid.NewGuid(), CustomerId, null, "customer@example.com", Address(), [], Now));

        Assert.Contains("item", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WhenValid_ShouldSumLineTotalsAndStartPending()
    {
        var orderId = Guid.NewGuid();
        var order = OrderAggregate.Create(
            orderId,
            CustomerId,
            null,
            "customer@example.com",
            Address(),
            [Item(orderId, 1, 20m), Item(orderId, 2, 15m)],
            Now);

        Assert.Equal(50m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0, order.TrackingStep);
        Assert.StartsWith("#GNJ-20260822-", order.Code);
    }

    [Theory]
    [InlineData(0, OrderStatus.Pending)]
    [InlineData(2, OrderStatus.Pending)]
    [InlineData(3, OrderStatus.Shipping)]
    [InlineData(4, OrderStatus.Shipping)]
    [InlineData(5, OrderStatus.Completed)]
    public void UpdateTracking_WhenStepIsValid_ShouldMapStatus(int step, OrderStatus expected)
    {
        var orderId = Guid.NewGuid();
        var order = OrderAggregate.Create(
            orderId, CustomerId, null, null, Address(), [Item(orderId, 1, 10m)], Now);

        order.UpdateTracking(step, step >= 3 ? "BR123" : null, Now.AddMinutes(1));

        Assert.Equal(step, order.TrackingStep);
        Assert.Equal(expected, order.Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void UpdateTracking_WhenStepIsOutsideRange_ShouldReject(int step)
    {
        var orderId = Guid.NewGuid();
        var order = OrderAggregate.Create(
            orderId, CustomerId, null, null, Address(), [Item(orderId, 1, 10m)], Now);

        Assert.Throws<DomainValidationException>(() => order.UpdateTracking(step, null, Now));
    }

    private static DeliveryAddress Address() => new(
        "Rua das Flores", "123", null, "Centro", "Curitiba", "PR", "80000-000", "BR");

    private static OrderItem Item(Guid orderId, int quantity, decimal price) =>
        OrderItem.Create(Guid.NewGuid(), orderId, "sku-12", "GANJJ T-Shirt", null, null,
            quantity, price, 0m, null, null);
}
