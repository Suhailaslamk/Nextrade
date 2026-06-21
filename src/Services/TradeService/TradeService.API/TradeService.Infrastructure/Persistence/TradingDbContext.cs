using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using TradingService.Domain.Entities;

namespace TradingService.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the Trading Service's own database
/// (trading_db), owning the Orders and OrderOutbox tables. Equivalent
/// in role to AuthDbContext in the Auth Service.
/// </summary>
public sealed class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderOutbox> OrderOutboxes => Set<OrderOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}