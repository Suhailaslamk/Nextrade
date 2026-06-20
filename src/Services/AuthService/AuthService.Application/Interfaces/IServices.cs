using AuthService.Domain.Entities;
using AuthService.Domain.Enums;

namespace AuthService.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> FindActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJwtService
{
    string GenerateAccessToken(User user);
    Task<string> GenerateRefreshTokenAsync();
    TokenValidationResult? ValidateAccessToken(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenBlacklistService
{
    Task BlacklistAsync(string jti, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default);
}

public sealed record TokenValidationResult(
    bool IsValid,
    Guid UserId,
    string Email,
    UserRole Role,
    string Jti
);