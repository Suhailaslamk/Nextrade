using Microsoft.EntityFrameworkCore;
using TradingService.Application.Common.Interfaces;
using TradingService.Application.Common.Models;
using TradingService.Domain.Entities;

namespace TradingService.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly TradingDbContext _dbContext;

    public OrderRepository(TradingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        _dbContext.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<(IReadOnlyList<Order> Orders, int TotalCount)> GetPagedAsync(
        OrderFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders.AsNoTracking().Where(o => o.UserId == filter.UserId);

        if (!string.IsNullOrWhiteSpace(filter.Symbol))
        {
            var symbol = filter.Symbol.Trim().ToUpperInvariant();
            query = query.Where(o => o.Symbol == symbol);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(o => o.SubmittedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (orders, totalCount);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await _dbContext.Orders.AddAsync(order, cancellationToken);

    public void Update(Order order) => _dbContext.Orders.Update(order);
}