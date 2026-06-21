namespace TradingService.Application.Features.Orders.Commands.SubmitOrder;

/// <summary>
/// Result returned from <see cref="SubmitOrderCommand"/>.
/// <see cref="IsDuplicate"/> indicates the request was a retry of an
/// already-processed idempotency key, in which case the original
/// order's id/status are returned rather than a new order being created.
/// </summary>
public sealed record SubmitOrderResult(
    Guid OrderId,
    string Status,
    bool IsDuplicate);
