namespace TradingService.Contracts.IntegrationEvents;

public static class KafkaTopics
{
    public const string OrdersSubmitted = "orders.submitted";
    public const string OrdersCancelled = "orders.cancelled";
}
