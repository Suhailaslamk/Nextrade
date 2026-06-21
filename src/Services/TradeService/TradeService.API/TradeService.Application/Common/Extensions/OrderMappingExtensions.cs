using TradingService.Application.DTOs;
using TradingService.Domain.Entities;

namespace TradingService.Application.Common.Extensions;

public static class OrderMappingExtensions
{
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto(
            order.Id,
            order.UserId,
            order.Symbol,
            order.Quantity,
            order.Price,
            order.Status.ToString()
        );
    }
}
