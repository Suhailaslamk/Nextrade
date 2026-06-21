using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TradingService.Application.Common.Models;

namespace TradingService.API.Extensions;

/// <summary>
/// Translates the Application layer's <see cref="Result"/> /
/// <see cref="Result{T}"/> outcomes into ASP.NET Core <see cref="ActionResult"/>
/// values, mapping <see cref="ErrorType"/> to the appropriate HTTP
/// status code and shaping failures as RFC 7807 ProblemDetails.
/// </summary>
public static class ResultExtensions
{
    public static ActionResult ToActionResult(this Result result, ControllerBase controller, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return new StatusCodeResult(successStatusCode);
        }

        return ToProblemResult(result.Error, controller);
    }

    public static ActionResult<T> ToActionResult<T>(
        this Result<T> result, ControllerBase controller, Func<T, ActionResult<T>>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is not null ? onSuccess(result.Value) : controller.Ok(result.Value);
        }

        return ToProblemResult(result.Error, controller);
    }

    private static ObjectResult ToProblemResult(Error error, ControllerBase controller)
    {
        var statusCode = MapStatusCode(error.Type);

        var problemDetails = new ProblemDetails
        {
            Title = MapTitle(error.Type),
            Detail = error.Description,
            Status = statusCode,
            Type = $"https://nextrade.dev/problems/{error.Code}",
            Instance = controller.HttpContext.Request.Path
        };

        problemDetails.Extensions["errorCode"] = error.Code;
        problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private static int MapStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string MapTitle(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Validation Failed",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Resource Not Found",
        ErrorType.Conflict => "Conflict",
        _ => "An Unexpected Error Occurred"
    };
}