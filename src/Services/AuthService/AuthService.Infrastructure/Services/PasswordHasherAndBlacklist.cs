using AuthService.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AuthService.Infrastructure.Services;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        // BCrypt.Verify is constant-time — safe against timing attacks
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

public sealed class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTokenBlacklistService> _logger;
    private const string Prefix = "auth:jti:blacklist:";

    public RedisTokenBlacklistService(
        IConnectionMultiplexer redis,
        ILogger<RedisTokenBlacklistService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task BlacklistAsync(string jti, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{Prefix}{jti}";
            await db.StringSetAsync(key, "1", expiry);
        }
        catch (RedisException ex)
        {
            // Log but do not throw — blacklist failure is non-fatal
            // The token will naturally expire; the window is limited (15 min)
            _logger.LogError(ex, "Failed to blacklist JTI {Jti} in Redis", jti);
        }
    }

    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{Prefix}{jti}";
            return await db.KeyExistsAsync(key);
        }
        catch (RedisException ex)
        {
            // Redis down → assume not blacklisted (fail-open for availability)
            // This is acceptable: token naturally expires in 15 min max
            _logger.LogWarning(ex, "Redis unavailable for blacklist check of JTI {Jti}. Failing open.", jti);
            return false;
        }
    }
}