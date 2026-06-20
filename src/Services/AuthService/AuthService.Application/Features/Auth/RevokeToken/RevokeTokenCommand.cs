using AuthService.Application.Common.Errors;
using AuthService.Application.Common.Interfaces;
using AuthService.Application.Common.Result;
using FluentValidation;
using MediatR;

namespace AuthService.Application.Features.Auth.RevokeToken;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RevokeTokenCommand(
    string RefreshToken,
    Guid RequestingUserId
) : IRequest<Result>;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty().WithMessage("Requesting user ID is required.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RevokeTokenCommandHandler
    : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public RevokeTokenCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<Result> Handle(
        RevokeTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the token
        var token = await _refreshTokenRepository.FindByTokenAsync(
            request.RefreshToken, cancellationToken);

        if (token is null)
            return AuthErrors.RefreshToken.NotFound;

        // 2. Ensure the requesting user owns this token
        if (token.UserId != request.RequestingUserId)
            return AuthErrors.RefreshToken.Invalid;

        // 3. Revoke (idempotent: already revoked returns error)
        if (token.IsRevoked)
            return Result.Success(); // Already revoked — treat as success (idempotent)

        token.Revoke();

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}