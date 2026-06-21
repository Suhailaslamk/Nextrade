namespace TradingService.Application.Common.Interfaces;

/// <summary>
/// Abstraction over Redis used for two distinct purposes:
///   1. General-purpose caching (e.g. GetOrderByIdQuery 30s TTL cache).
///   2. A SETNX-based distributed lock primitive used by
///      OutboxRelayWorker to ensure only one replica relays a given
///      batch of outbox records to Kafka at a time.
/// </summary>
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a distributed lock using Redis SETNX
    /// semantics (SET key value NX PX ttl). Returns a non-empty lock
    /// token if acquired, which must be passed to
    /// <see cref="ReleaseLockAsync"/> to release it safely — this
    /// prevents a slow caller from releasing a lock acquired by a
    /// different, later owner after its own lock expired.
    /// </summary>
    Task<string?> AcquireLockAsync(string lockKey, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a lock previously acquired with <see cref="AcquireLockAsync"/>,
    /// atomically (via Lua script) verifying that the caller still owns it
    /// before deleting the key.
    /// </summary>
    Task<bool> ReleaseLockAsync(string lockKey, string lockToken, CancellationToken cancellationToken = default);
}
