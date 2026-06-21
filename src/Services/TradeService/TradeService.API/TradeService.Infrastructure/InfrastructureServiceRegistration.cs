using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using TradingService.Application.Common.Interfaces;
using TradingService.Infrastructure.BackgroundServices;
using TradingService.Infrastructure.Caching;
using TradingService.Infrastructure.Grpc;
using TradingService.Infrastructure.Messaging;
using TradingService.Infrastructure.Persistence;
using TradingService.Infrastructure.Persistence.Repositories;

namespace TradingService.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddCaching(services, configuration);
        AddMessaging(services, configuration);
        AddGrpcClients(services);
        AddBackgroundServices(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TradingDb")
            ?? throw new InvalidOperationException("Connection string 'TradingDb' is not configured.");

        services.AddDbContext<TradingDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.MigrationsAssembly(typeof(TradingDbContext).Assembly.FullName);
            }));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>()
                ?? new RedisSettings();

            return ConnectionMultiplexer.Connect(redisSettings.ConnectionString);
        });

        services.AddSingleton<IRedisCacheService, RedisCacheService>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
    }

    private static void AddGrpcClients(IServiceCollection services)
    {
        // TODO: replace with a real Grpc.Net.Client-backed implementation
        // once the Risk Service ships risk_service.proto over gRPC.
        services.AddSingleton<IRiskGrpcClient, RiskGrpcClientStub>();
    }

    private static void AddBackgroundServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxRelaySettings>(configuration.GetSection(OutboxRelaySettings.SectionName));
        services.AddHostedService<OutboxRelayWorker>();
    }
}