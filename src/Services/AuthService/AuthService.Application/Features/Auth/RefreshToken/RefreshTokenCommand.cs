using AuthService.Application.Common.Errors;
using AuthService.Application.Common.Interfaces;
using AuthService.Application.Common.Result;
using AuthService.Application.Features.Auth.Login;
using AuthService.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace AuthService.Application.Features.Auth.RefreshToken;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RefreshTokenCommand(
    string RefreshToken
) : IRequest<Result<RefreshTokenResponse>>;

// ── Response ──────────────────────────────────────────────────────────────────

public sealed record RefreshTokenResponse(
    string AccessToken,
    string NewRefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt
);

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IJwtService jwtService,
        IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _jwtService = jwtService;
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the existing refresh token
        var existingToken = await _refreshTokenRepository.FindByTokenAsync(
            request.RefreshToken, cancellationToken);

        if (existingToken is null)
            return AuthErrors.RefreshToken.NotFound;

        // 2. Validate token state
        if (existingToken.IsRevoked)
            return AuthErrors.RefreshToken.Revoked;

        if (existingToken.IsExpired)
            return AuthErrors.RefreshToken.Expired;

        // 3. Load the owning user
        var user = await _userRepository.FindByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null)
            return AuthErrors.User.NotFound;

        if (!user.IsActive)
            return AuthErrors.User.Inactive;

        // 4. Rotate refresh token — revoke old, issue new
        existingToken.Revoke();

        var rawNewRefreshToken = await _jwtService.GenerateRefreshTokenAsync();
        var newExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.ExpiryDays);
        var newRefreshToken = AuthService.Domain.Entities.RefreshToken.Create(
            user.Id, rawNewRefreshToken, newExpiresAt);

        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        // 5. Issue new access token
        var accessToken = _jwtService.GenerateAccessToken(user);

        return new RefreshTokenResponse(
            AccessToken: accessToken,
            NewRefreshToken: rawNewRefreshToken,
            AccessTokenExpiresAt: DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpiresAt: newExpiresAt);
    }
}