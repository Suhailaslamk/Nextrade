using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TradingService.Application.Common.Interfaces;

namespace TradingService.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="IRedisCacheService"/>,
/// providing both general caching and a SETNX-based distributed lock
/// used by OutboxRelayWorker to coordinate across replicas.
/// </summary>
public sealed class RedisCacheService : IRedisCacheService
{
    // Lua script: only delete the key if its value still matches the
    // token we set when acquiring it. This prevents replica A from
    // releasing a lock that replica B has since legitimately acquired
    // after A's lock expired (classic distributed-lock pitfall).
    private const string ReleaseLockScript = """
        if redis.call("GET", KEYS[1]) == ARGV[1] then
            return redis.call("DEL", KEYS[1])
        else
            return 0
        end
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisSettings> settings,
        ILogger<RedisCacheService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _settings = settings.Value;
        _logger = logger;
    }

    private IDatabase Database => _connectionMultiplexer.GetDatabase();

    private string Prefixed(string key) => $"{_settings.KeyPrefix}{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var value = await Database.StringGetAsync(Prefixed(key));
            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            // Cache is an optimization, not a correctness dependency —
            // degrade gracefully to a cache miss rather than failing
            // the request.
            _logger.LogWarning(ex, "Redis unavailable while reading key {Key}; treating as cache miss", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            await Database.StringSetAsync(Prefixed(key), serialized, expiry);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable while writing key {Key}; skipping cache write", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await Database.KeyDeleteAsync(Prefixed(key));
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable while removing key {Key}", key);
        }
    }

    public async Task<string?> AcquireLockAsync(
        string lockKey, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");

        // SET key value NX PX ttl — atomic test-and-set.
        var acquired = await Database.StringSetAsync(
            Prefixed(lockKey), token, ttl, When.NotExists);

        if (!acquired)
        {
            return null;
        }

        return token;
    }

    public async Task<bool> ReleaseLockAsync(
        string lockKey, string lockToken, CancellationToken cancellationToken = default)
    {
        var result = await Database.ScriptEvaluateAsync(
            ReleaseLockScript,
            new RedisKey[] { Prefixed(lockKey) },
            new RedisValue[] { lockToken });

        return (long)result! == 1L;
    }
}