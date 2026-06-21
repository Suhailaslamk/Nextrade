using TradingService.Domain.Common;
using TradingService.Domain.Enums;
using TradingService.Domain.Events;
using TradingService.Domain.Exceptions;

namespace TradingService.Domain.Entities;

/// <summary>
/// Aggregate root representing a client order within the Trading Service
/// bounded context. Prices and quantities are stored as scaled int64
/// values (e.g. ₹1500.50 → 150050) to avoid IEEE-754 floating point
/// comparison defects, consistent with the platform-wide pricing
/// convention used by the Matching Engine.
/// </summary>
public sealed class Order : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Symbol { get; private set; } = default!;
    public OrderSide Side { get; private set; }
    public OrderType Type { get; private set; }

    /// <summary>Scaled int64 limit price. 0 is valid for MARKET orders.</summary>
    public long Price { get; private set; }
    public long Quantity { get; private set; }
    public long FilledQuantity { get; private set; }
    public OrderStatus Status { get; private set; }

    /// <summary>Client-supplied key used to make order submission idempotent.</summary>
    public string IdempotencyKey { get; private set; } = default!;

    public DateTime SubmittedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public long RemainingQuantity => Quantity - FilledQuantity;
    public bool IsFilled => FilledQuantity >= Quantity;

    // EF Core requires a parameterless constructor.
    private Order() { }

    /// <summary>
    /// Factory method enforcing all invariants required to bring a new
    /// order into existence. Raises <see cref="OrderSubmittedDomainEvent"/>.
    /// </summary>
    public static Order Submit(
        Guid userId,
        string symbol,
        OrderSide side,
        OrderType type,
        long price,
        long quantity,
        string idempotencyKey)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be greater than zero.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));

        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = symbol.Trim().ToUpperInvariant(),
            Side = side,
            Type = type,
            Price = price,
            Quantity = quantity,
            FilledQuantity = 0,
            Status = OrderStatus.Open,
            IdempotencyKey = idempotencyKey,
            SubmittedAt = now,
            UpdatedAt = now
        };

        order.AddDomainEvent(new OrderSubmittedDomainEvent(
            order.Id, order.UserId, order.Symbol, order.Side, order.Type, order.Price, order.Quantity));

        return order;
    }

    /// <summary>
    /// Cancels the order. Only permitted while the order is Open or
    /// Partially filled. Raises <see cref="OrderCancelledDomainEvent"/>.
    /// </summary>
    public void Cancel()
    {
        if (Status is not (OrderStatus.Open or OrderStatus.Partial))
            throw new InvalidOrderStateException(Id, Status.ToString(), "cancelled");

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderCancelledDomainEvent(Id, UserId, Symbol));
    }

    /// <summary>
    /// Verifies that the given user is the owner of this order, throwing
    /// a domain exception if not. Used before mutating operations.
    /// </summary>
    public void EnsureOwnedBy(Guid userId)
    {
        if (UserId != userId)
            throw new OrderOwnershipException(Id, userId);
    }

    /// <summary>
    /// Applies a partial or complete fill to the order. Reserved for
    /// future use once fill events are consumed back from Settlement.
    /// </summary>
    public void ApplyFill(long fillQuantity)
    {
        if (fillQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(fillQuantity));

        if (Status is OrderStatus.Cancelled or OrderStatus.Rejected)
            throw new InvalidOrderStateException(Id, Status.ToString(), "filled");

        if (FilledQuantity + fillQuantity > Quantity)
            throw new InvalidOperationException("Fill quantity exceeds remaining order quantity.");

        FilledQuantity += fillQuantity;
        Status = IsFilled ? OrderStatus.Filled : OrderStatus.Partial;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        if (Status != OrderStatus.Open)
            throw new InvalidOrderStateException(Id, Status.ToString(), "rejected");

        Status = OrderStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }
}