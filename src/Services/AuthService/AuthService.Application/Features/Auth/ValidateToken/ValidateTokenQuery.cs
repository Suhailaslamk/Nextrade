using AuthService.Application.Common.Errors;
using AuthService.Application.Common.Interfaces;
using AuthService.Application.Common.Result;
using AuthService.Domain.Enums;
using MediatR;

namespace AuthService.Application.Features.Auth.ValidateToken;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ValidateTokenQuery(string Token) : IRequest<Result<ValidateTokenResponse>>;

// ── Response ──────────────────────────────────────────────────────────────────

public sealed record ValidateTokenResponse(
    bool IsValid,
    Guid UserId,
    string Email,
    UserRole Role,
    IReadOnlyList<string> Roles
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ValidateTokenQueryHandler
    : IRequestHandler<ValidateTokenQuery, Result<ValidateTokenResponse>>
{
    private readonly IJwtService _jwtService;
    private readonly ITokenBlacklistService _tokenBlacklist;

    public ValidateTokenQueryHandler(
        IJwtService jwtService,
        ITokenBlacklistService tokenBlacklist)
    {
        _jwtService = jwtService;
        _tokenBlacklist = tokenBlacklist;
    }

    public async Task<Result<ValidateTokenResponse>> Handle(
        ValidateTokenQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Validate token signature and claims
        var validationResult = _jwtService.ValidateAccessToken(request.Token);
        if (validationResult is null || !validationResult.IsValid)
            return AuthErrors.Token.Invalid;

        // 2. Check Redis blacklist (for revoked tokens via logout)
        var isBlacklisted = await _tokenBlacklist.IsBlacklistedAsync(
            validationResult.Jti, cancellationToken);

        if (isBlacklisted)
            return AuthErrors.Token.Invalid;

        return new ValidateTokenResponse(
            IsValid: true,
            UserId: validationResult.UserId,
            Email: validationResult.Email,
            Role: validationResult.Role,
            Roles: new[] { validationResult.Role.ToString().ToUpperInvariant() });
    }
}