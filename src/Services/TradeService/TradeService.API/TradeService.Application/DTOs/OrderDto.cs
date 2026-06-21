using System;

namespace TradingService.Application.DTOs;

public sealed record OrderDto(
    Guid OrderId,
    Guid UserId,
    string Symbol,
    long Quantity,
    long Price,
    string Status
);
