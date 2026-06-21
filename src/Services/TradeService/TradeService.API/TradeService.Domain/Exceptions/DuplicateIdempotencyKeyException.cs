namespace TradingService.Domain.Exceptions;

/// <summary>
/// Raised when an order submission is attempted with an idempotency key
/// that maps to an existing order owned by a *different* user, which
/// indicates a key collision rather than a legitimate retry.
/// </summary>
public sealed class DuplicateIdempotencyKeyException : DomainException
{
    public DuplicateIdempotencyKeyException(string idempotencyKey)
        : base(
            "order.duplicate_idempotency_key",
            $"Idempotency key '{idempotencyKey}' has already been used.")
    {
    }
}