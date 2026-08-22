namespace Ganjj.Order.Application.Checkout;

public sealed record CartSnapshot(IReadOnlyList<CartItemSnapshot> Items);

public sealed record CartItemSnapshot(
    long ProductId,
    string ProductName,
    string? Size,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice);
