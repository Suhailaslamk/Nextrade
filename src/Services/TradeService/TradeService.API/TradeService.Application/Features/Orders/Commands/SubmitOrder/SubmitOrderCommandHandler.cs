using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using TradingService.Application.Common.Interfaces;
using TradingService.Application.Common.Models;
using TradingService.Contracts.Grpc;
using TradingService.Contracts.IntegrationEvents;
using TradingService.Domain.Entities;

namespace TradingService.Application.Features.Orders.Commands.SubmitOrder;

/// <summary>
/// Handles <see cref="SubmitOrderCommand"/>:
///   1. Validation (pipeline behavior, already passed by the time we get here)
///   2. Duplicate idempotency-key check
///   3. Pre-trade risk check via gRPC (stub for now)
///   4. Persist Order + OrderOutbox in a single database transaction
///   5. Return the new order id
/// The order is never published to Kafka directly from here — that is
/// the responsibility of OutboxRelayWorker, reading the OrderOutbox
/// row written in the same transaction as the order itself.
/// </summary>
public sealed class SubmitOrderCommandHandler
    : IRequestHandler<SubmitOrderCommand, Result<SubmitOrderResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRiskGrpcClient _riskGrpcClient;
    private readonly ILogger<SubmitOrderCommandHandler> _logger;

    public SubmitOrderCommandHandler(
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        IRiskGrpcClient riskGrpcClient,
        ILogger<SubmitOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _riskGrpcClient = riskGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitOrderResult>> Handle(
        SubmitOrderCommand request, CancellationToken cancellationToken)
    {
        // 2. Duplicate idempotency-key check — return the existing
        // order instead of creating a new one.
        var existingOrder = await _orderRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, cancellationToken);

        if (existingOrder is not null)
        {
            _logger.LogInformation(
                "Duplicate submission detected for IdempotencyKey {IdempotencyKey}; returning existing order {OrderId}",
                request.IdempotencyKey, existingOrder.Id);

            return Result.Success(new SubmitOrderResult(
                existingOrder.Id, existingOrder.Status.ToString(), IsDuplicate: true));
        }

        // 3. Pre-trade risk check.
        var orderId = Guid.NewGuid();
        var riskResponse = await _riskGrpcClient.CheckOrderAsync(
            new CheckOrderRequest(
                request.UserId,
                orderId,
                request.Symbol,
                request.Side.ToString(),
                request.Price,
                request.Quantity),
            cancellationToken);

        if (!riskResponse.Approved)
        {
            _logger.LogWarning(
                "Order rejected by risk check for user {UserId} symbol {Symbol}: {Reason}",
                request.UserId, request.Symbol, riskResponse.RejectReason);

            return Result.Failure<SubmitOrderResult>(
                Error.Conflict("order.risk_rejected", riskResponse.RejectReason ?? "Order rejected by risk check."));
        }

        // 4-6. Persist Order + OrderOutbox atomically inside a retryable
        // execution strategy, satisfying SqlServerRetryingExecutionStrategy.
        Order? persistedOrder = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var order = Domain.Entities.Order.Submit(
                request.UserId,
                request.Symbol,
                request.Side,
                request.Type,
                request.Price,
                request.Quantity,
                request.IdempotencyKey);

            await _orderRepository.AddAsync(order, ct);

            var integrationEvent = new OrderSubmittedIntegrationEvent(
                order.Id,
                order.UserId,
                order.Symbol,
                order.Side.ToString(),
                order.Type.ToString(),
                order.Price,
                order.Quantity,
                order.IdempotencyKey,
                order.SubmittedAt);

            var payload = JsonSerializer.Serialize(integrationEvent, JsonOptions);
            var outboxRecord = OrderOutbox.Create(order.Id, "OrderSubmitted", payload);

            await _outboxRepository.AddAsync(outboxRecord, ct);

            persistedOrder = order;
        }, cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} submitted for user {UserId} symbol {Symbol}",
            persistedOrder!.Id, persistedOrder.UserId, persistedOrder.Symbol);

        return Result.Success(new SubmitOrderResult(persistedOrder.Id, persistedOrder.Status.ToString(), IsDuplicate: false));
    }
}
