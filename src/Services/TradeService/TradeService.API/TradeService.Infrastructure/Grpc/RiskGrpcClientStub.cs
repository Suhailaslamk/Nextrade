using Microsoft.Extensions.Logging;
using TradingService.Application.Common.Interfaces;
using TradingService.Contracts.Grpc;

namespace TradingService.Infrastructure.Grpc;

/// <summary>
/// Stub implementation of <see cref="IRiskGrpcClient"/>, standing in
/// for the real Risk Service gRPC client until that service exists
/// (see Phase 3 of the NexTrade implementation roadmap). Always
/// approves, after simulating realistic network latency, so the
/// SubmitOrder flow can be developed and tested end-to-end today.
///
/// Swap-out plan: once Risk Service ships its risk_service.proto
/// contract over gRPC, replace the registration of this class in
/// InfrastructureServiceRegistration with a Grpc.Net.Client-backed
/// implementation that calls the generated RiskService.RiskServiceClient
/// — IRiskGrpcClient's shape does not need to change.
/// </summary>
public sealed class RiskGrpcClientStub : IRiskGrpcClient
{
    private readonly ILogger<RiskGrpcClientStub> _logger;

    public RiskGrpcClientStub(ILogger<RiskGrpcClientStub> logger)
    {
        _logger = logger;
    }

    public async Task<CheckOrderResponse> CheckOrderAsync(
        CheckOrderRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[STUB] RiskService.CheckOrder called for user {UserId} order {OrderId} symbol {Symbol}",
            request.UserId, request.OrderId, request.Symbol);

        // Simulate realistic gRPC round-trip latency.
        await Task.Delay(15, cancellationToken);

        return new CheckOrderResponse(Approved: true, RejectReason: null);
    }
}