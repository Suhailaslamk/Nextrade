using System;

namespace TradingService.Contracts.Grpc;

/// <summary>
/// Plain C# mirror of the <c>CheckOrderRequest</c> Protobuf message
/// (see Protos/risk_service.proto). Used by <c>IRiskGrpcClient</c> while
/// the real Risk Service gRPC client has not yet been code-generated /
/// wired up. Once Grpc.Tools is added to TradingService.Contracts.csproj
/// with a &lt;Protobuf Include="Protos/risk_service.proto" GrpcServices="Client" /&gt;
/// item, these POCOs can be replaced by the generated message types
/// with no change to the IRiskGrpcClient contract shape.
/// </summary>
public sealed record CheckOrderRequest(
    Guid UserId,
    Guid OrderId,
    string Symbol,
    string Side,
    long Price,
    long Quantity);

public sealed record CheckOrderResponse(
    bool Approved,
    string? RejectReason);

public sealed record LockBalanceRequest(
    Guid UserId,
    long Amount);

public sealed record LockBalanceResponse(
    bool Success,
    long AvailableAfter);
