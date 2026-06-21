using System;

namespace TradingService.Contracts.IntegrationEvents;

public sealed record OrderCancelledIntegrationEvent(Guid OrderId, Guid UserId, string Symbol, DateTime CancelledAt);

public sealed record OrderSubmittedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    string Symbol,
    string Side,
    string Type,
    long Price,
    long Quantity,
    string IdempotencyKey,
    DateTime SubmittedAt);
