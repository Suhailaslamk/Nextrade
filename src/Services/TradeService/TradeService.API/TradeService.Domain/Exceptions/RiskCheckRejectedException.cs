namespace TradingService.Domain.Exceptions;

/// <summary>
/// Raised when the Risk Service rejects an order during pre-trade checks.
/// </summary>
public sealed class RiskCheckRejectedException : DomainException
{
    public string Reason { get; }

    public RiskCheckRejectedException(string reason)
        : base("order.risk_rejected", $"Order rejected by risk check: {reason}")
    {
        Reason = reason;
    }
}