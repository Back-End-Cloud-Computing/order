using Ganjj.Order.Domain.Orders;

namespace Ganjj.Order.Domain.UnitTests;

public sealed class OrderItemTests
{
    [Fact]
    public void Create_WhenQuantityIsZero_ShouldRejectItem()
    {
        var exception = Assert.Throws<DomainValidationException>(() => CreateItem(quantity: 0));

        Assert.Contains("Quantity", exception.Message);
    }

    [Fact]
    public void Create_WhenDiscountExceedsListPrice_ShouldRejectItem()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            CreateItem(listPrice: 20m, discount: 20.01m));

        Assert.Contains("discount", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WhenSnapshotIsValid_ShouldCalculateFinalPriceAndLineTotal()
    {
        var item = CreateItem(quantity: 3, listPrice: 19.99m, discount: 2.50m);

        Assert.Equal(17.49m, item.UnitFinalPrice);
        Assert.Equal(52.47m, item.LineTotal);
    }

    private static OrderItem CreateItem(
        int quantity = 1,
        decimal listPrice = 10m,
        decimal discount = 0m) =>
        OrderItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            12,
            "GANJJ T-Shirt",
            null,
            null,
            quantity,
            listPrice,
            discount,
            null,
            null);
}
