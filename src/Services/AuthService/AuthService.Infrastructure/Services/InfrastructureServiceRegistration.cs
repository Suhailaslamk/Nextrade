using AuthService.Application.Common.Interfaces;
using AuthService.Application.Features.Auth.Login;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AuthService.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── SQL Server / EF Core ──────────────────────────────────────────────

        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("AuthDb"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
                });
        });

        // ── Repositories ──────────────────────────────────────────────────────

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // ── JWT ───────────────────────────────────────────────────────────────

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtService, JwtService>();

        // ── Options ───────────────────────────────────────────────────────────

        services.Configure<RefreshTokenOptions>(
            configuration.GetSection(RefreshTokenOptions.SectionName));

        // ── Password Hasher ───────────────────────────────────────────────────

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // ── Redis ─────────────────────────────────────────────────────────────

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string 'Redis' is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();

        return services;
    }
}