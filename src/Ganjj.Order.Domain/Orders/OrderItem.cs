namespace Ganjj.Order.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public long ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? Size { get; private set; }
    public string? ImageUrl { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitListPrice { get; private set; }
    public decimal UnitDiscount { get; private set; }
    public decimal UnitFinalPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public string? PromotionId { get; private set; }
    public string? PromotionName { get; private set; }

    public static OrderItem Create(
        Guid id,
        Guid orderId,
        long productId,
        string productName,
        string? size,
        string? imageUrl,
        int quantity,
        decimal unitListPrice,
        decimal unitDiscount,
        string? promotionId,
        string? promotionName)
    {
        if (id == Guid.Empty || orderId == Guid.Empty)
        {
            throw new DomainValidationException("Item and Order identifiers are required.");
        }

        if (productId <= 0)
        {
            throw new DomainValidationException("ProductId must be positive.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainValidationException("ProductName is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainValidationException("Quantity must be positive.");
        }

        if (unitListPrice < 0 || unitDiscount < 0)
        {
            throw new DomainValidationException("Price and discount cannot be negative.");
        }

        if (unitDiscount > unitListPrice)
        {
            throw new DomainValidationException("Unit discount cannot exceed list price.");
        }

        var finalPrice = decimal.Round(unitListPrice - unitDiscount, 2, MidpointRounding.AwayFromZero);

        return new OrderItem
        {
            Id = id,
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            Size = string.IsNullOrWhiteSpace(size) ? null : size.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            Quantity = quantity,
            UnitListPrice = decimal.Round(unitListPrice, 2, MidpointRounding.AwayFromZero),
            UnitDiscount = decimal.Round(unitDiscount, 2, MidpointRounding.AwayFromZero),
            UnitFinalPrice = finalPrice,
            LineTotal = decimal.Round(finalPrice * quantity, 2, MidpointRounding.AwayFromZero),
            PromotionId = string.IsNullOrWhiteSpace(promotionId) ? null : promotionId.Trim(),
            PromotionName = string.IsNullOrWhiteSpace(promotionName) ? null : promotionName.Trim()
        };
    }
}
