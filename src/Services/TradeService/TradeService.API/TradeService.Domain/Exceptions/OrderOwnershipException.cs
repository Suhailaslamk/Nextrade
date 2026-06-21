namespace TradingService.Domain.Exceptions;

/// <summary>
/// Raised when a user attempts to act on an order they do not own.
/// </summary>
public sealed class OrderOwnershipException : DomainException
{
    public OrderOwnershipException(Guid orderId, Guid userId)
        : base("order.forbidden", $"User '{userId}' does not own order '{orderId}'.")
    {
    }
}