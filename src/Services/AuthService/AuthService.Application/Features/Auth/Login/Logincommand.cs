using AuthService.Application.Common.Errors;
using AuthService.Application.Common.Interfaces;
using AuthService.Application.Common.Result;
using AuthService.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace AuthService.Application.Features.Auth.Login;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record LoginCommand(
    string Email,
    string Password
) : IRequest<Result<LoginResponse>>;

// ── Response ──────────────────────────────────────────────────────────────────

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string FullName,
    string Role
);

// ── Options ───────────────────────────────────────────────────────────────────

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";
    public int ExpiryDays { get; set; } = 7;
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find user by email
        var user = await _userRepository.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null)
            return AuthErrors.User.InvalidCredentials;

        // 2. Check user is active
        if (!user.IsActive)
            return AuthErrors.User.Inactive;

        // 3. Verify password — constant-time comparison inside BCrypt
        var isValid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!isValid)
            return AuthErrors.User.InvalidCredentials;

        // 4. Generate access token (RS256 signed JWT, 15 min TTL)
        var accessToken = _jwtService.GenerateAccessToken(user);

        // 5. Generate and persist refresh token (7 day TTL)
        var rawRefreshToken = await _jwtService.GenerateRefreshTokenAsync();
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.ExpiryDays);
        var refreshTokenEntity = AuthService.Domain.Entities.RefreshToken.Create(user.Id, rawRefreshToken, expiresAt);

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAt: DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpiresAt: expiresAt,
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString().ToUpperInvariant());
    }
}