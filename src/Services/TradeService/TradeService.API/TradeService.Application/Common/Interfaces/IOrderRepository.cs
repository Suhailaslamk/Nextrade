using TradingService.Application.Common.Models;
using TradingService.Domain.Entities;
using TradingService.Domain.Enums;

namespace TradingService.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction over the Orders table. Implemented by
/// TradingService.Infrastructure using EF Core / SQL Server.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Orders, int TotalCount)> GetPagedAsync(
        OrderFilter filter, CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// EF Core change tracking handles updates automatically for
    /// tracked entities, but this is exposed explicitly for clarity
    /// and to support repository implementations backed by a detached
    /// context (e.g. in background workers).
    /// </summary>
    void Update(Order order);
}
