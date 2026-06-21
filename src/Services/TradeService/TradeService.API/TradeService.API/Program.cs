using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TradingService.API.Extensions;
using TradingService.API.Middleware;
using TradingService.Application;
using TradingService.Infrastructure;
using TradingService.Infrastructure.Persistence;

// Bootstrap logger captures any failures that occur before the full
// Serilog pipeline (reading appsettings, DI, etc.) is configured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting TradingService.API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "TradingService"));

    // ---- Application layers ----
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ---- API concerns ----
    builder.Services.AddControllers();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorization();
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddObservability(builder.Configuration);
    builder.Services.AddTradingHealthChecks(builder.Configuration);

    // RFC 7807 ProblemDetails for both MVC model-validation failures
    // and the global exception handler below.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    // Apply pending EF Core migrations automatically in non-Production
    // environments for developer convenience; Production deployments
    // should run migrations explicitly as part of the release pipeline.
    if (!app.Environment.IsProduction())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "TradingService API v1");
        });
    }

    app.UseExceptionHandler(); // delegates to GlobalExceptionHandler
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "TradingService.API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Exposed for WebApplicationFactory-based integration testing.
public partial class Program
{
}