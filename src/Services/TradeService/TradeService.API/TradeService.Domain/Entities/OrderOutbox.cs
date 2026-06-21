namespace TradingService.Domain.Entities;

/// <summary>
/// Transactional outbox record. Persisted in the same database
/// transaction as the <see cref="Order"/> write it accompanies,
/// guaranteeing that an event is never lost even if the subsequent
/// Kafka publish fails. A background relay (OutboxRelayWorker) polls
/// unprocessed rows and publishes them to Kafka, then stamps
/// <see cref="ProcessedAt"/>.
/// </summary>
public sealed class OrderOutbox
{
    public Guid Id { get; private set; }

    /// <summary>The order this event relates to.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Logical event name, e.g. "OrderSubmitted" or "OrderCancelled".</summary>
    public string EventType { get; private set; } = default!;

    /// <summary>JSON-serialized integration event payload.</summary>
    public string Payload { get; private set; } = default!;

    public DateTime CreatedAt { get; private set; }

    /// <summary>Set once the relay worker has successfully published this record to Kafka.</summary>
    public DateTime? ProcessedAt { get; private set; }

    public bool IsProcessed => ProcessedAt.HasValue;

    // EF Core requires a parameterless constructor.
    private OrderOutbox() { }

    public static OrderOutbox Create(Guid orderId, string eventType, string payload)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType is required.", nameof(eventType));

        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload is required.", nameof(payload));

        return new OrderOutbox
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            EventType = eventType,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };
    }

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;
}