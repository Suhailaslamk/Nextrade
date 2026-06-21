namespace TradingService.Infrastructure.Caching;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Prefix applied to every key written by this service, to avoid collisions with other services sharing the same Redis instance.</summary>
    public string KeyPrefix { get; set; } = "trading:";
}