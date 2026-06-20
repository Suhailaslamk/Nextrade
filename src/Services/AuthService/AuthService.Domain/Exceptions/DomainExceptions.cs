using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AuthService.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class UserAlreadyExistsException : DomainException
{
    public UserAlreadyExistsException(string email)
        : base($"A user with email '{email}' already exists.") { }
}

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("The provided credentials are invalid.") { }
}

public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(Guid userId)
        : base($"User with ID '{userId}' was not found.") { }

    public UserNotFoundException(string email)
        : base($"User with email '{email}' was not found.") { }
}

public sealed class RefreshTokenNotFoundException : DomainException
{
    public RefreshTokenNotFoundException()
        : base("The provided refresh token was not found or has expired.") { }
}

public sealed class RefreshTokenRevokedException : DomainException
{
    public RefreshTokenRevokedException()
        : base("The provided refresh token has already been revoked.") { }
}

public sealed class RefreshTokenExpiredException : DomainException
{
    public RefreshTokenExpiredException()
        : base("The provided refresh token has expired.") { }
}

public sealed class UserInactiveException : DomainException
{
    public UserInactiveException()
        : base("The user account has been deactivated.") { }
}