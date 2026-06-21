namespace TradingService.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Kafka producer used by OutboxRelayWorker to
/// publish integration events. Implementations are expected to
/// configure <c>acks=all</c> and <c>enable.idempotence=true</c> so
/// that publishing is exactly-once at the producer level.
/// </summary>
public interface IKafkaProducer
{
    /// <summary>
    /// Publishes <paramref name="payload"/> (already serialized JSON)
    /// to <paramref name="topic"/> under the given partition key.
    /// Throws if the broker does not acknowledge the write so the
    /// caller (OutboxRelayWorker) can leave the outbox record
    /// unprocessed for a later retry.
    /// </summary>
    Task PublishAsync(
        string topic,
        string key,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}
