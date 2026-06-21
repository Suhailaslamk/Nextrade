using TradingService.Application.Common.Interfaces;
using TradingService.Application.Common.Models;
using TradingService.Application.DTOs;
using TradingService.Domain.Enums;

namespace TradingService.Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Lists the calling user's orders, optionally filtered by symbol
/// and/or status, paginated. Not cached — results are user-specific
/// and frequently changing.
/// </summary>
public sealed record GetOrdersQuery(
    Guid UserId,
    string? Symbol,
    OrderStatus? Status,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<OrderDto>>;