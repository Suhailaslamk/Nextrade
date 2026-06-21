using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingService.Domain.Entities;
using TradingService.Domain.Enums;

namespace TradingService.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Quantity", "[Quantity] > 0");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.UserId).IsRequired();

        builder.Property(o => o.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(o => o.Side)
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => (OrderSide)Enum.Parse(typeof(OrderSide), v, true));

        builder.Property(o => o.Type)
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => (OrderType)Enum.Parse(typeof(OrderType), v, true));

        builder.Property(o => o.Price)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(o => o.Quantity).IsRequired();
        builder.Property(o => o.FilledQuantity).IsRequired().HasDefaultValue(0L);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => (OrderStatus)Enum.Parse(typeof(OrderStatus), v, true))
            .HasDefaultValue(OrderStatus.Open);

        builder.Property(o => o.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.SubmittedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(o => o.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.HasIndex(o => o.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UQ_Orders_IdempotencyKey");

        builder.HasIndex(o => o.UserId)
            .HasDatabaseName("IX_Orders_UserId");

        builder.HasIndex(o => new { o.Symbol, o.Status })
            .HasDatabaseName("IX_Orders_Symbol_Status");

        builder.HasIndex(o => o.SubmittedAt)
            .IsDescending()
            .HasDatabaseName("IX_Orders_SubmittedAt");

        // Domain events are an in-memory concern, never persisted.
        builder.Ignore(o => o.DomainEvents);
    }
}