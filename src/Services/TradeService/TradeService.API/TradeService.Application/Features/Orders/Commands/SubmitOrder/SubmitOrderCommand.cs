using System;
using TradingService.Application.Common.Interfaces;
using TradingService.Domain.Enums;

namespace TradingService.Application.Features.Orders.Commands.SubmitOrder;

public sealed record SubmitOrderCommand(
    Guid UserId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    long Price,
    long Quantity,
    string IdempotencyKey) : ICommand<SubmitOrderResult>;
