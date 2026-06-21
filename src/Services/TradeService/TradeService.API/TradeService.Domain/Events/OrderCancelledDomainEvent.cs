using TradingService.Domain.Common;

namespace TradingService.Domain.Events;

public sealed class OrderCancelledDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;

    public Guid OrderId { get; }
    public Guid UserId { get; }
    public string Symbol { get; }

    public OrderCancelledDomainEvent(Guid orderId, Guid userId, string symbol)
    {
        OrderId = orderId;
        UserId = userId;
        Symbol = symbol;
    }
}