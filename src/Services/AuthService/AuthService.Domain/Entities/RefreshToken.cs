using AuthService.Domain.Exceptions;

namespace AuthService.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation property
    public User? User { get; private set; }

    // EF Core constructor
    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (expiresAt <= DateTime.UtcNow) throw new ArgumentException("ExpiresAt must be in the future.", nameof(expiresAt));

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            RevokedAt = null,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke()
    {
        if (IsRevoked)
            throw new RefreshTokenRevokedException();

        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }

    public void EnsureValid()
    {
        if (IsRevoked) throw new RefreshTokenRevokedException();
        if (IsExpired) throw new RefreshTokenExpiredException();
    }
}