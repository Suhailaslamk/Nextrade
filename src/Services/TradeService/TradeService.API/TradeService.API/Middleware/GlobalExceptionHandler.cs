using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using System.Diagnostics;
using TradingService.Domain.Exceptions;

namespace TradingService.API.Middleware;

/// <summary>
/// Catches any exception that escapes the MediatR pipeline (i.e. was
/// not already handled via the Result&lt;T&gt; pattern) and converts it
/// into an RFC 7807 ProblemDetails response, using ASP.NET Core 8's
/// built-in <see cref="IExceptionHandler"/> extension point. Registered
/// alongside <c>AddProblemDetails()</c> in Program.cs.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, code) = MapException(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Request {Method} {Path} failed with {StatusCode}",
                httpContext.Request.Method, httpContext.Request.Path, statusCode);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Type = $"https://nextrade.dev/problems/{code}",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Code) MapException(Exception exception) => exception switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation Failed", "validation.failed"),
        OrderNotFoundException domainEx => (StatusCodes.Status404NotFound, "Resource Not Found", domainEx.Code),
        OrderOwnershipException domainEx => (StatusCodes.Status403Forbidden, "Forbidden", domainEx.Code),
        InvalidOrderStateException domainEx => (StatusCodes.Status409Conflict, "Conflict", domainEx.Code),
        DuplicateIdempotencyKeyException domainEx => (StatusCodes.Status409Conflict, "Conflict", domainEx.Code),
        RiskCheckRejectedException domainEx => (StatusCodes.Status409Conflict, "Conflict", domainEx.Code),
        DomainException domainEx => (StatusCodes.Status400BadRequest, "Domain Rule Violated", domainEx.Code),
        _ => (StatusCodes.Status500InternalServerError, "An Unexpected Error Occurred", "internal_server_error")
    };
}