using TradingService.Domain.Entities;

namespace TradingService.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction over the OrderOutbox table, used both by
/// command handlers (to enqueue events transactionally with the order
/// write) and by OutboxRelayWorker (to poll and mark records processed).
/// </summary>
public interface IOutboxRepository
{
    Task AddAsync(OrderOutbox outboxRecord, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="batchSize"/> unprocessed records,
    /// oldest first, for relay to Kafka.
    /// </summary>
    Task<IReadOnlyList<OrderOutbox>> GetUnprocessedBatchAsync(
        int batchSize, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid outboxId, CancellationToken cancellationToken = default);
}
