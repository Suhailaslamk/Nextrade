using TradingService.Application.Common.Interfaces;
using TradingService.Application.DTOs;

namespace TradingService.Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// Fetches a single order by id, scoped to the requesting user.
/// Cached in Redis for 30 seconds under key "order:{orderId}".
/// </summary>
public sealed record GetOrderByIdQuery(Guid OrderId, Guid UserId) : IQuery<OrderDto>;