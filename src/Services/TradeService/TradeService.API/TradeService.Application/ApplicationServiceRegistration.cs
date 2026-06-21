using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TradingService.Application.Common.Behaviors;

namespace TradingService.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceRegistration).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        services.AddValidatorsFromAssembly(assembly);

        // Pipeline order matters: outermost first.
        //   1. Logging          — wraps everything, including failures.
        //   2. UnhandledException — captures truly unexpected exceptions for diagnostics.
        //   3. Validation        — short-circuits on bad input before any handler logic runs.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}