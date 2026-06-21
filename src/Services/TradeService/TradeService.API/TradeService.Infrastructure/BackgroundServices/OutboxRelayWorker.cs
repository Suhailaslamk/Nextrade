using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TradingService.Application.Common.Interfaces;
using TradingService.Contracts.IntegrationEvents;
using TradingService.Infrastructure.Caching;
using TradingService.Infrastructure.Messaging;
using TradingService.Infrastructure.Persistence.Repositories;

namespace TradingService.Infrastructure.BackgroundServices;

/// <summary>
/// Polls the OrderOutbox table for unprocessed records and relays them
/// to Kafka, implementing the transactional outbox pattern's "relay"
/// half. Multiple replicas of the Trading Service may run this worker
/// concurrently; a Redis SETNX-based distributed lock ensures only one
/// replica drains a given poll cycle at a time, preventing duplicate
/// publishes under normal operation (Kafka idempotent producer plus
/// downstream consumer idempotency provide defense in depth on top of
/// this).
/// </summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRedisCacheService _redisCacheService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly OutboxRelaySettings _settings;
    private readonly ILogger<OutboxRelayWorker> _logger;

    public OutboxRelayWorker(
        IServiceScopeFactory scopeFactory,
        IRedisCacheService redisCacheService,
        IKafkaProducer kafkaProducer,
        IOptions<OutboxRelaySettings> settings,
        ILogger<OutboxRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _redisCacheService = redisCacheService;
        _kafkaProducer = kafkaProducer;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxRelayWorker starting. PollInterval={PollIntervalMs}ms BatchSize={BatchSize}",
            _settings.PollIntervalMs, _settings.BatchSize);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.PollIntervalMs));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunRelayCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a single bad cycle crash the worker — log
                // and try again on the next tick.
                _logger.LogError(ex, "Unhandled error in OutboxRelayWorker cycle");
            }
        }

        _logger.LogInformation("OutboxRelayWorker stopping.");
    }

    private async Task RunRelayCycleAsync(CancellationToken cancellationToken)
    {
        var lockTtl = TimeSpan.FromSeconds(_settings.LockTtlSeconds);
        var lockToken = await _redisCacheService.AcquireLockAsync(_settings.LockKey, lockTtl, cancellationToken);

        if (lockToken is null)
        {
            // Another replica currently owns the relay lock; skip this cycle.
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

            var batch = await outboxRepository.GetUnprocessedBatchAsync(_settings.BatchSize, cancellationToken);

            if (batch.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Relaying {Count} outbox record(s) to Kafka", batch.Count);

            foreach (var record in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RelayRecordAsync(record, outboxRepository, cancellationToken);
            }
        }
        finally
        {
            await _redisCacheService.ReleaseLockAsync(_settings.LockKey, lockToken, cancellationToken);
        }
    }

    private async Task RelayRecordAsync(
        Domain.Entities.OrderOutbox record,
        IOutboxRepository outboxRepository,
        CancellationToken cancellationToken)
    {
        var topic = ResolveTopic(record.EventType);

        if (topic is null)
        {
            _logger.LogError(
                "Outbox record {OutboxId} has unrecognized EventType {EventType}; leaving unprocessed for investigation",
                record.Id, record.EventType);
            return;
        }

        var partitionKey = ExtractSymbolForPartitioning(record.Payload) ?? record.OrderId.ToString();

        try
        {
            await _kafkaProducer.PublishAsync(
                topic,
                partitionKey,
                record.Payload,
                headers: new Dictionary<string, string>
                {
                    ["event-type"] = record.EventType,
                    ["outbox-id"] = record.Id.ToString()
                },
                cancellationToken);

            await outboxRepository.MarkProcessedAsync(record.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            // Publish failed (broker unavailable, timeout, etc). Leave
            // ProcessedAt null so the record is retried on the next
            // poll cycle — at-least-once delivery by design.
            _logger.LogError(
                ex, "Failed to relay outbox record {OutboxId} ({EventType}) to {Topic}; will retry",
                record.Id, record.EventType, topic);
        }
    }

    private static string? ResolveTopic(string eventType) => eventType switch
    {
        "OrderSubmitted" => KafkaTopics.OrdersSubmitted,
        "OrderCancelled" => KafkaTopics.OrdersCancelled,
        _ => null
    };

    /// <summary>
    /// Pulls the "symbol" field out of the serialized payload so Kafka
    /// messages partition by instrument symbol (matching consumers'
    /// expectations for ordered per-symbol processing), without taking
    /// a hard dependency on a specific event DTO shape here.
    /// </summary>
    private static string? ExtractSymbolForPartitioning(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("symbol", out var symbolElement))
            {
                return symbolElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall through to caller's fallback key.
        }

        return null;
    }
}