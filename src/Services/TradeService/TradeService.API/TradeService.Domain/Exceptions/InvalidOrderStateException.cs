namespace TradingService.Domain.Exceptions;

/// <summary>
/// Raised when an operation is attempted against an order whose current
/// status does not permit it (e.g. cancelling an already-filled order).
/// </summary>
public sealed class InvalidOrderStateException : DomainException
{
    public InvalidOrderStateException(Guid orderId, string currentStatus, string attemptedAction)
        : base(
            "order.invalid_state",
            $"Order '{orderId}' cannot be {attemptedAction} while in status '{currentStatus}'.")
    {
    }
}