using System.Security.Claims;

namespace TradingService.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated user's id from the JWT's "sub" (or
    /// fallback NameIdentifier) claim, as issued by the Auth Service.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject, out var userId))
        {
            throw new InvalidOperationException("The authenticated principal does not contain a valid user id claim.");
        }

        return userId;
    }
}