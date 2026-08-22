using Ganjj.Order.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<OrderAggregate>
{
    public void Configure(EntityTypeBuilder<OrderAggregate> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_TrackingStep", "[TrackingStep] BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_Orders_Total", "[Total] >= 0");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(order => order.Code).IsUnique();
        builder.Property(order => order.CustomerName).HasMaxLength(200);
        builder.Property(order => order.CustomerEmail).HasMaxLength(320);
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(order => order.Total).HasPrecision(18, 2);
        builder.Property(order => order.TrackingNumber).HasMaxLength(100);
        builder.Property(order => order.Version).IsRowVersion();
        builder.HasIndex(order => order.QuoteId).IsUnique().HasFilter("[QuoteId] IS NOT NULL");
        builder.HasIndex(order => new { order.CustomerId, order.CreatedAt });
        builder.HasIndex(order => order.CreatedAt);

        builder.OwnsOne(order => order.DeliveryAddress, address =>
        {
            address.Property(value => value.Street).HasColumnName("DeliveryStreet").HasMaxLength(200).IsRequired();
            address.Property(value => value.Number).HasColumnName("DeliveryNumber").HasMaxLength(30).IsRequired();
            address.Property(value => value.Complement).HasColumnName("DeliveryComplement").HasMaxLength(100);
            address.Property(value => value.District).HasColumnName("DeliveryDistrict").HasMaxLength(100).IsRequired();
            address.Property(value => value.City).HasColumnName("DeliveryCity").HasMaxLength(100).IsRequired();
            address.Property(value => value.State).HasColumnName("DeliveryState").HasMaxLength(50).IsRequired();
            address.Property(value => value.PostalCode).HasColumnName("DeliveryPostalCode").HasMaxLength(20).IsRequired();
            address.Property(value => value.Country).HasColumnName("DeliveryCountry").HasMaxLength(2).IsRequired();
        });

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
