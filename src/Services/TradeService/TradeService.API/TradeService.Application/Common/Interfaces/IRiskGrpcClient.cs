using TradingService.Contracts.Grpc;

namespace TradingService.Application.Common.Interfaces;

/// <summary>
/// Client abstraction for the (future) Risk Service gRPC endpoint.
/// SubmitOrderCommandHandler calls <see cref="CheckOrderAsync"/> as a
/// pre-trade check before persisting an order. Until the Risk Service
/// is implemented (Phase 3 of the roadmap), TradingService.Infrastructure
/// provides a stub implementation that always approves.
/// </summary>
public interface IRiskGrpcClient
{
    Task<CheckOrderResponse> CheckOrderAsync(CheckOrderRequest request, CancellationToken cancellationToken = default);
}
