using MediatR;
using Microsoft.Extensions.Logging;
using TradingService.Application.Common.Extensions;
using TradingService.Application.Common.Interfaces;
using TradingService.Application.Common.Models;
using TradingService.Application.DTOs;

namespace TradingService.Application.Features.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IOrderRepository _orderRepository;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IRedisCacheService cache,
        ILogger<GetOrderByIdQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"order:{request.OrderId}";

        var cached = await _cache.GetAsync<OrderDto>(cacheKey, cancellationToken);
        if (cached is not null && cached.UserId == request.UserId)
        {
            _logger.LogDebug("Cache hit for order {OrderId}", request.OrderId);
            return Result.Success(cached);
        }

        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(Error.NotFound(
                "order.not_found", $"Order '{request.OrderId}' was not found."));
        }

        if (order.UserId != request.UserId)
        {
            return Result.Failure<OrderDto>(Error.Forbidden(
                "order.forbidden", "You do not have permission to view this order."));
        }

        var dto = order.ToDto();

        await _cache.SetAsync(cacheKey, dto, CacheTtl, cancellationToken);

        return Result.Success(dto);
    }
}