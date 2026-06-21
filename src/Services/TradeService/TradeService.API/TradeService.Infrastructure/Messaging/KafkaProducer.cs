using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection.PortableExecutable;
using TradingService.Application.Common.Interfaces;

namespace TradingService.Infrastructure.Messaging;

/// <summary>
/// Confluent.Kafka-backed implementation of <see cref="IKafkaProducer"/>.
/// Configured for <c>acks=all</c> + <c>enable.idempotence=true</c> so a
/// successful publish is durably acknowledged by the broker (and the
/// in-sync replica set) before <see cref="PublishAsync"/> returns —
/// this is what lets OutboxRelayWorker safely mark a record processed
/// only after a true delivery guarantee.
/// </summary>
public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            ClientId = settings.Value.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = settings.Value.MessageTimeoutMs,
            CompressionType = CompressionType.Snappy
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
                _logger.LogError("Kafka producer error: {Reason} (IsFatal: {IsFatal})", error.Reason, error.IsFatal))
            .Build();
    }

    public async Task PublishAsync(
        string topic,
        string key,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = payload
        };

        if (headers is { Count: > 0 })
        {
            message.Headers = new Headers();
            foreach (var (headerKey, headerValue) in headers)
            {
                message.Headers.Add(headerKey, System.Text.Encoding.UTF8.GetBytes(headerValue));
            }
        }

        try
        {
            var deliveryResult = await _producer.ProduceAsync(topic, message, cancellationToken);

            _logger.LogInformation(
                "Published to {Topic} [partition {Partition} offset {Offset}] key={Key}",
                topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value, key);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex, "Failed to publish to {Topic} key={Key}: {Reason}", topic, key, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}