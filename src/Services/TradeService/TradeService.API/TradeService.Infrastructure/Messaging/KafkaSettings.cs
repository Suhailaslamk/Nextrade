namespace TradingService.Infrastructure.Messaging;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ClientId { get; set; } = "trading-service";

    /// <summary>Max time to wait for broker acknowledgement before considering a publish failed.</summary>
    public int MessageTimeoutMs { get; set; } = 10_000;
}