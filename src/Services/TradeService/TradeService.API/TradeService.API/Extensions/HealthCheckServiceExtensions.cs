using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradingService.API.Extensions;

public static class HealthCheckServiceExtensions
{
    public static IServiceCollection AddTradingHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var sqlConnectionString = configuration.GetConnectionString("TradingDb")!;
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";

        services.AddHealthChecks()
            .AddSqlServer(
                sqlConnectionString,
                name: "sql-server",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" })
            .AddRedis(
                redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" });

        return services;
    }
}