using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TradingService.Application.Common.Interfaces;
using TradingService.Application.Common.Models;
using TradingService.Contracts.IntegrationEvents;
using TradingService.Domain.Entities;
using TradingService.Domain.Exceptions;

namespace TradingService.Application.Features.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        IRedisCacheService cache,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. Load order.
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(Error.NotFound(
                "order.not_found", $"Order '{request.OrderId}' was not found."));
        }

        // 2. Verify ownership.
        if (order.UserId != request.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to cancel order {OrderId} owned by {OwnerId}",
                request.UserId, request.OrderId, order.UserId);

            return Result.Failure(Error.Forbidden(
                "order.forbidden", "You do not have permission to cancel this order."));
        }

        try
        {
            // Wrap all DB mutations inside ExecuteInTransactionAsync so the
            // transaction is owned by the execution strategy, not user code.
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // 3. Status guard + transition (domain method enforces Open/Partial invariant).
                order.Cancel();
                _orderRepository.Update(order);

                // 4. Outbox record for orders.cancelled.
                var integrationEvent = new OrderCancelledIntegrationEvent(
                    order.Id, order.UserId, order.Symbol, DateTime.UtcNow);

                var payload = JsonSerializer.Serialize(integrationEvent, JsonOptions);
                var outboxRecord = OrderOutbox.Create(order.Id, "OrderCancelled", payload);
                await _outboxRepository.AddAsync(outboxRecord, ct);
            }, cancellationToken);

            // 5. Invalidate the per-order cache entry written by GetOrderByIdQuery.
            await _cache.RemoveAsync($"order:{order.Id}", cancellationToken);

            _logger.LogInformation("Order {OrderId} cancelled by user {UserId}", order.Id, order.UserId);

            return Result.Success();
        }
        catch (InvalidOrderStateException ex)
        {
            return Result.Failure(Error.Conflict(ex.Code, ex.Message));
        }
    }
}