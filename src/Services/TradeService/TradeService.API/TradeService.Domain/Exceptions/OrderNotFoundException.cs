namespace TradingService.Domain.Exceptions;

public sealed class OrderNotFoundException : DomainException
{
    public Guid OrderId { get; }

    public OrderNotFoundException(Guid orderId)
        : base("order.not_found", $"Order '{orderId}' was not found.")
    {
        OrderId = orderId;
    }
}