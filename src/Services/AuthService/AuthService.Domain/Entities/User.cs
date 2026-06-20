using AuthService.Domain.Enums;
using AuthService.Domain.Events;
using AuthService.Domain.Exceptions;

namespace AuthService.Domain.Entities;

public sealed class User
{
    private readonly List<DomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core concurrency token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation property
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
    private readonly List<RefreshToken> _refreshTokens = new();

    // EF Core constructor
    private User() { }

    public static User Create(string email, string passwordHash, string fullName, UserRole role = UserRole.Trader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant().Trim(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user._domainEvents.Add(new UserRegisteredDomainEvent(user.Id, user.Email, user.FullName));

        return user;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePasswordHash(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);
        PasswordHash = newHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void PromoteToRole(UserRole newRole)
    {
        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureActive()
    {
        if (!IsActive)
            throw new UserInactiveException();
    }

    public IReadOnlyList<DomainEvent> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}