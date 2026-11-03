namespace Ganjj.Order.Application.Checkout;

public sealed record CartSnapshot(
    IReadOnlyList<CartItemSnapshot> Items,
    string? CouponCode = null);

public sealed record CartItemSnapshot(
    string ProductId,
    string ProductName,
    string? Color,
    string? ImageUrl,
    int Quantity,
    decimal UnitListPrice,
    decimal UnitFinalPrice);
