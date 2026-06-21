using MediatR;
using TradingService.Application.Common.Extensions;
using TradingService.Application.Common.Interfaces;
using TradingService.Application.Common.Models;
using TradingService.Application.DTOs;

namespace TradingService.Application.Features.Orders.Queries.GetOrders;

public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, Result<PagedResult<OrderDto>>>
{
    private const int MaxPageSize = 100;

    private readonly IOrderRepository _orderRepository;

    public GetOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<PagedResult<OrderDto>>> Handle(
        GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var filter = new OrderFilter(request.UserId, request.Symbol, request.Status, page, pageSize);

        var (orders, totalCount) = await _orderRepository.GetPagedAsync(filter, cancellationToken);

        var dtos = orders.Select(o => o.ToDto()).ToList();

        var paged = new PagedResult<OrderDto>(dtos, totalCount, page, pageSize);

        return Result.Success(paged);
    }
}
