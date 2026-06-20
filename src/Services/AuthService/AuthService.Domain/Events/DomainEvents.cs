using MediatR;

namespace AuthService.Domain.Events;

public abstract class DomainEvent : INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public sealed class UserRegisteredDomainEvent : DomainEvent
{
    public Guid UserId { get; }
    public string Email { get; }
    public string FullName { get; }

    public UserRegisteredDomainEvent(Guid userId, string email, string fullName)
    {
        UserId = userId;
        Email = email;
        FullName = fullName;
    }
}