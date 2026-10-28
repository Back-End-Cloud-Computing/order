using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ganjj.Order.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.EventType).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(message => message.CorrelationId).HasMaxLength(128);
        builder.Property(message => message.LastError).HasMaxLength(2000);
        builder.HasIndex(message => new { message.PublishedAt, message.NextAttemptAt });
        builder.HasIndex(message => message.LockedUntil);
    }
}
