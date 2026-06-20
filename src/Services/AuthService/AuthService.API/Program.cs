using AuthService.API.Grpc;
using AuthService.API.Middleware;
using AuthService.Application.DependencyInjection;
using AuthService.Infrastructure.DependencyInjection;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using System.Security.Cryptography;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

// ── Bootstrap Logger (before DI) ─────────────────────────────────────────────

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting NexTrade AuthService");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────

    builder.Host.UseSerilog((context, services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "AuthService")
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.Seq(
                context.Configuration["Serilog:SeqUrl"] ?? "http://localhost:5341");
    });

    // ── Generate Dev JWT Keys if needed ───────────────────────────────────────

    var jwtOptionsSection = builder.Configuration.GetSection(JwtOptions.SectionName);
    var publicKeyPem = jwtOptionsSection["PublicKeyPem"];
    var privateKeyPem = jwtOptionsSection["PrivateKeyPem"];

    // Generate temporary RSA keys in development if not provided or placeholders
    if (builder.Environment.IsDevelopment() && (
        string.IsNullOrWhiteSpace(publicKeyPem) || publicKeyPem.StartsWith("REPLACE_WITH") ||
        string.IsNullOrWhiteSpace(privateKeyPem) || privateKeyPem.StartsWith("REPLACE_WITH")))
    {
        Log.Information("Generating temporary RSA key pair for development environment.");
        using var tempRsa = RSA.Create(2048);
        privateKeyPem = tempRsa.ExportPkcs8PrivateKeyPem();
        publicKeyPem = tempRsa.ExportSubjectPublicKeyInfoPem();

        // Update configuration and local variables
        builder.Configuration[$"{JwtOptions.SectionName}:PrivateKeyPem"] = privateKeyPem;
        builder.Configuration[$"{JwtOptions.SectionName}:PublicKeyPem"] = publicKeyPem;
    }

    // ── Application and Infrastructure Layers ─────────────────────────────────

    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ── Authentication — RS256 JWT ────────────────────────────────────────────

    var publicKeyConfig = builder.Configuration[$"{JwtOptions.SectionName}:PublicKeyPem"]
        ?? throw new InvalidOperationException("JWT:PublicKeyPem is not configured.");

    publicKeyPem = publicKeyConfig.Trim();

    // If configuration contains a path to a file, read the file contents.
    if (File.Exists(publicKeyPem))
    {
        publicKeyPem = File.ReadAllText(publicKeyPem).Trim();
    }
    else if (publicKeyPem.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
    {
        var path = publicKeyPem.Substring("file:".Length);
        if (File.Exists(path))
            publicKeyPem = File.ReadAllText(path).Trim();
    }

    // If the configured value is not a PEM string, generate a temporary RSA key pair for development/testing.
    if (!publicKeyPem.StartsWith("-----BEGIN"))
    {
        Log.Information("Configured JWT public key is not PEM; generating temporary RSA key pair.");
        using var tempRsa = RSA.Create(2048);
        publicKeyPem = tempRsa.ExportSubjectPublicKeyInfoPem();
        // Update configuration so subsequent runs can reuse or for consistency
        builder.Configuration[$"{JwtOptions.SectionName}:PublicKeyPem"] = publicKeyPem;
    }


    var rsa = RSA.Create();
    rsa.ImportFromPem(publicKeyPem);

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration[$"{JwtOptions.SectionName}:Issuer"] ?? "nextrade-auth",
                ValidateAudience = true,
                ValidAudience = builder.Configuration[$"{JwtOptions.SectionName}:Audience"] ?? "nextrade-api",
                ValidateLifetime = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = ctx =>
                {
                    Log.Warning("JWT authentication failed: {Error}", ctx.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireTrader",
            p => p.RequireRole("TRADER", "MARKET_MAKER", "ADMIN"));
        options.AddPolicy("RequireAdmin",
            p => p.RequireRole("ADMIN"));
        options.AddPolicy("RequireMarketMaker",
            p => p.RequireRole("MARKET_MAKER", "ADMIN"));
    });

    // ── Controllers ───────────────────────────────────────────────────────────

    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    // ── gRPC ──────────────────────────────────────────────────────────────────

    builder.Services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    });

    // ── Swagger / OpenAPI ─────────────────────────────────────────────────────

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "NexTrade Auth Service",
            Version = "v1",
            Description = "Authentication and identity service for NexTrade."
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT access token: Bearer {token}"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ── Health Checks ─────────────────────────────────────────────────────────

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AuthDbContext>("authdb")
        .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", "redis");

    // ── OpenTelemetry ─────────────────────────────────────────────────────────

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: "AuthService",
                serviceVersion: "1.0.0"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(opts =>
            {
                opts.SetDbStatementForText = builder.Environment.IsDevelopment();
            })
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(
                    builder.Configuration["Otel:Endpoint"] ?? "http://jaeger:4317");
            }));

    // ── Exception Handler ─────────────────────────────────────────────────────

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // ── CORS (for development) ────────────────────────────────────────────────

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins("http://localhost:3000", "http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    // ─────────────────────────────────────────────────────────────────────────

    var app = builder.Build();

    // ── Apply EF Core Migrations on Startup (dev / staging only) ─────────────

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied.");
    }

    // ── Middleware Pipeline ───────────────────────────────────────────────────

    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService v1");
            options.RoutePrefix = "swagger";
        });
        app.UseCors();
    }

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGrpcService<AuthGrpcServiceImpl>();
    app.MapHealthChecks("/health");

    Log.Information("NexTrade AuthService started on {Urls}", string.Join(", ", app.Urls));

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "NexTrade AuthService terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }