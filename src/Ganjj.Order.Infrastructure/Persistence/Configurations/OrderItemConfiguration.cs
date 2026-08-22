using Ganjj.Order.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ganjj.Order.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_OrderItems_Prices",
                "[UnitListPrice] >= 0 AND [UnitDiscount] >= 0 AND [UnitFinalPrice] >= 0 AND [LineTotal] >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ProductName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Size).HasMaxLength(50);
        builder.Property(item => item.ImageUrl).HasMaxLength(2048);
        builder.Property(item => item.UnitListPrice).HasPrecision(18, 2);
        builder.Property(item => item.UnitDiscount).HasPrecision(18, 2);
        builder.Property(item => item.UnitFinalPrice).HasPrecision(18, 2);
        builder.Property(item => item.LineTotal).HasPrecision(18, 2);
        builder.Property(item => item.PromotionId).HasMaxLength(100);
        builder.Property(item => item.PromotionName).HasMaxLength(250);
        builder.HasIndex(item => item.OrderId);
    }
}
