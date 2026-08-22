using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ganjj.Order.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("IdempotencyRequests");
        builder.HasKey(request => request.Key);
        builder.Property(request => request.Key).HasMaxLength(100);
        builder.Property(request => request.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(request => request.OrderId);
    }
}
