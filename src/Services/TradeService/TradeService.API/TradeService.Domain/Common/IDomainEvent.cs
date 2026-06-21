using TradingService.Domain.Common;

using MediatR;

namespace TradingService.Domain.Common;

/// <summary>
/// Marker interface for in-process domain events raised by aggregates.
/// Domain events are dispatched via MediatR's INotification pipeline
/// from within the same bounded context (e.g. for audit logging).
/// They are distinct from integration events, which are relayed to
/// Kafka through the transactional outbox.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}