using TradingService.Domain.Common;
using TradingService.Domain.Enums;

namespace TradingService.Domain.Events;

/// <summary>
/// In-process notification raised when a new order is successfully
/// created in the domain. Consumed by MediatR notification handlers
/// within the Trading Service (e.g. for structured audit logging).
/// This is NOT what gets published to Kafka — that is the
/// OrderSubmittedIntegrationEvent, relayed via the transactional outbox.
/// </summary>
public sealed class OrderSubmittedDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;

    public Guid OrderId { get; }
    public Guid UserId { get; }
    public string Symbol { get; }
    public OrderSide Side { get; }
    public OrderType Type { get; }
    public long Price { get; }
    public long Quantity { get; }

    public OrderSubmittedDomainEvent(
        Guid orderId,
        Guid userId,
        string symbol,
        OrderSide side,
        OrderType type,
        long price,
        long quantity)
    {
        OrderId = orderId;
        UserId = userId;
        Symbol = symbol;
        Side = side;
        Type = type;
        Price = price;
        Quantity = quantity;
    }
}