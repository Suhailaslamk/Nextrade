namespace TradingService.Domain.Enums;

/// <summary>
/// Lifecycle state of an order as tracked by the Trading Service.
/// Fill-related transitions (Partial/Filled) are driven asynchronously
/// by events originating from the Matching Engine / Settlement Service.
/// </summary>
public enum OrderStatus
{
    Open = 0,
    Partial = 1,
    Filled = 2,
    Cancelled = 3,
    Rejected = 4
}