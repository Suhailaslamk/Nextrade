namespace TradingService.Domain.Exceptions;

/// <summary>
/// Base type for all exceptions raised by violations of domain invariants.
/// Caught by the API's global exception middleware and translated into
/// an RFC 7807 ProblemDetails response.
/// </summary>
public abstract class DomainException : Exception
{
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}