using TradingService.Domain.Enums;

namespace TradingService.API.Models;

public sealed record SubmitOrderRequest(
    string Symbol,
    OrderSide Side,
    OrderType Type,
    long Price,
    long Quantity,
    string IdempotencyKey);