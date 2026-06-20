using AuthService.Application.Features.Auth.GetCurrentUser;
using AuthService.Application.Features.Auth.ValidateToken;
using AuthService.Contracts.Grpc;
using Grpc.Core;
using MediatR;
using ValidateTokenResponse = AuthService.Contracts.Grpc.ValidateTokenResponse;

namespace AuthService.API.Grpc;

public sealed class AuthGrpcServiceImpl : AuthGrpcService.AuthGrpcServiceBase
{
    private readonly ISender _sender;
    private readonly ILogger<AuthGrpcServiceImpl> _logger;

    public AuthGrpcServiceImpl(ISender sender, ILogger<AuthGrpcServiceImpl> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public override async Task<ValidateTokenResponse> ValidateToken(
        ValidateTokenRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return new ValidateTokenResponse { Valid = false };
        }

        var query = new ValidateTokenQuery(request.Token);
        var result = await _sender.Send(query, context.CancellationToken);

        if (result.IsFailure)
        {
            _logger.LogDebug("Token validation failed: {Error}", result.Error);
            return new ValidateTokenResponse { Valid = false };
        }

        var response = new ValidateTokenResponse
        {
            Valid = true,
            UserId = result.Value!.UserId.ToString(),
            Email = result.Value.Email,
        };
        response.Roles.AddRange(result.Value.Roles);

        return response;
    }

    public override async Task<GetUserResponse> GetUser(
        GetUserRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "user_id must be a valid GUID."));
        }

        var query = new GetCurrentUserQuery(userId);
        var result = await _sender.Send(query, context.CancellationToken);

        if (result.IsFailure)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                result.Error.Description));
        }

        var user = result.Value!;

        return new GetUserResponse
        {
            UserId = user.UserId.ToString(),
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = new DateTimeOffset(user.CreatedAt).ToUnixTimeSeconds(),
            Roles = { user.Role },
        };
    }
}