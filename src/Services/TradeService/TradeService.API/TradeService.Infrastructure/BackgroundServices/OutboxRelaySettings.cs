namespace TradingService.Infrastructure.BackgroundServices;

public sealed class OutboxRelaySettings
{
    public const string SectionName = "OutboxRelay";

    /// <summary>How often the worker polls for unprocessed outbox records.</summary>
    public int PollIntervalMs { get; set; } = 500;

    /// <summary>Maximum number of outbox records relayed per poll cycle.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>TTL for the Redis distributed lock guarding a single relay cycle.</summary>
    public int LockTtlSeconds { get; set; } = 30;

    public string LockKey { get; set; } = "outbox:relay:lock";
}