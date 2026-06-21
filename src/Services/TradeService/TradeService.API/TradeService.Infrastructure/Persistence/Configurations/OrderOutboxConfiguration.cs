using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingService.Domain.Entities;

namespace TradingService.Infrastructure.Persistence.Configurations;

public sealed class OrderOutboxConfiguration : IEntityTypeConfiguration<OrderOutbox>
{
    public void Configure(EntityTypeBuilder<OrderOutbox> builder)
    {
        builder.ToTable("OrderOutbox");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.OrderId).IsRequired();

        builder.Property(o => o.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(o => o.ProcessedAt);

        // Polled constantly by OutboxRelayWorker filtering on
        // ProcessedAt IS NULL, ordered by CreatedAt — this composite
        // index keeps that query index-only.
        builder.HasIndex(o => new { o.ProcessedAt, o.CreatedAt })
            .HasDatabaseName("IX_OrderOutbox_ProcessedAt_CreatedAt");

        builder.HasIndex(o => o.OrderId)
            .HasDatabaseName("IX_OrderOutbox_OrderId");
    }
}