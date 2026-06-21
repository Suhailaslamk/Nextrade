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
    var publicKeyPath = jwtOptionsSection["PublicKeyPath"];
    var privateKeyPath = jwtOptionsSection["PrivateKeyPath"];

    // Generate temporary RSA keys in development if files are missing
    if (builder.Environment.IsDevelopment() && (
        string.IsNullOrWhiteSpace(publicKeyPath) || !File.Exists(publicKeyPath) ||
        string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath)))
    {
        Log.Information("Generating temporary RSA key pair for development environment.");
        
        var keysDir = Path.Combine(builder.Environment.ContentRootPath, "Keys");
        if (!Directory.Exists(keysDir))
            Directory.CreateDirectory(keysDir);

        var devPrivateKeyPath = Path.Combine(keysDir, "auth-private-key.dev.pem");
        var devPublicKeyPath = Path.Combine(keysDir, "auth-public-key.dev.pem");

        using var tempRsa = RSA.Create(2048);
        var privatePem = tempRsa.ExportPkcs8PrivateKeyPem();
        var publicPem = tempRsa.ExportSubjectPublicKeyInfoPem();

        File.WriteAllText(devPrivateKeyPath, privatePem);
        File.WriteAllText(devPublicKeyPath, publicPem);

        // Update configuration
        builder.Configuration[$"{JwtOptions.SectionName}:PrivateKeyPath"] = devPrivateKeyPath;
        builder.Configuration[$"{JwtOptions.SectionName}:PublicKeyPath"] = devPublicKeyPath;
    }

    // ── Application and Infrastructure Layers ─────────────────────────────────

    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ── Authentication — RS256 JWT ────────────────────────────────────────────

    var configuredPublicKeyPath = builder.Configuration[$"{JwtOptions.SectionName}:PublicKeyPath"]
        ?? throw new InvalidOperationException("JWT:PublicKeyPath is not configured.");

    configuredPublicKeyPath = configuredPublicKeyPath.Trim();

    if (!File.Exists(configuredPublicKeyPath))
    {
        throw new InvalidOperationException($"Public key file not found at '{configuredPublicKeyPath}'");
    }

    var publicKeyPemContent = File.ReadAllText(configuredPublicKeyPath).Trim();

    var rsa = RSA.Create();
    rsa.ImportFromPem(publicKeyPemContent);

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