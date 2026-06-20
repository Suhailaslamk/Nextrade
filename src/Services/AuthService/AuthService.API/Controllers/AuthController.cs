using AuthService.Application.Common.Result;
using AuthService.Application.Features.Auth.GetCurrentUser;
using AuthService.Application.Features.Auth.Login;
using AuthService.Application.Features.Auth.RefreshToken;
using AuthService.Application.Features.Auth.Register;
using AuthService.Application.Features.Auth.RevokeToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.API.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <response code="201">User created successfully.</response>
    /// <response code="409">Email already exists.</response>
    /// <response code="422">Validation failed.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            Email: request.Email,
            Password: request.Password,
            FullName: request.FullName);

        var result = await _sender.Send(command, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: response => CreatedAtAction(
                nameof(Me),
                new { },
                response),
            onFailure: error => Problem(
                title: error.Code,
                detail: error.Description,
                statusCode: ErrorTypeToStatusCode(error.Type)));
    }

    /// <summary>
    /// Login with email and password.
    /// </summary>
    /// <response code="200">Login successful. Returns access and refresh tokens.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(
            Email: request.Email,
            Password: request.Password);

        var result = await _sender.Send(command, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: response => Ok(response),
            onFailure: error => Problem(
                title: error.Code,
                detail: error.Description,
                statusCode: ErrorTypeToStatusCode(error.Type)));
    }

    /// <summary>
    /// Exchange a refresh token for a new access token and rotated refresh token.
    /// </summary>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(RefreshToken: request.RefreshToken);
        var result = await _sender.Send(command, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: response => Ok(response),
            onFailure: error => Problem(
                title: error.Code,
                detail: error.Description,
                statusCode: ErrorTypeToStatusCode(error.Type)));
    }

    /// <summary>
    /// Revoke a refresh token (logout from the current device).
    /// </summary>
    /// <response code="204">Token revoked.</response>
    /// <response code="401">Unauthorized or token not found.</response>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var command = new RevokeTokenCommand(
            RefreshToken: request.RefreshToken,
            RequestingUserId: userId.Value);

        var result = await _sender.Send(command, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: () => NoContent(),
            onFailure: error => Problem(
                title: error.Code,
                detail: error.Description,
                statusCode: ErrorTypeToStatusCode(error.Type)));
    }

    /// <summary>
    /// Get the currently authenticated user's profile.
    /// </summary>
    /// <response code="200">User profile.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var query = new GetCurrentUserQuery(userId.Value);
        var result = await _sender.Send(query, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: response => Ok(response),
            onFailure: error => Problem(
                title: error.Code,
                detail: error.Description,
                statusCode: ErrorTypeToStatusCode(error.Type)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(subClaim, out var id) ? id : null;
    }

    private static int ErrorTypeToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public sealed record RegisterUserRequest(string Email, string Password, string FullName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record RevokeTokenRequest(string RefreshToken);