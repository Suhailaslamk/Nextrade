using AuthService.Application.Common.Result;

namespace AuthService.Application.Common.Errors;

public static class AuthErrors
{
    public static class User
    {
        public static readonly Error NotFound =
            Error.NotFound("User.NotFound", "The user was not found.");

        public static readonly Error EmailAlreadyExists =
            Error.Conflict("User.EmailAlreadyExists", "A user with this email already exists.");

        public static readonly Error InvalidCredentials =
            Error.Unauthorized("User.InvalidCredentials", "The provided credentials are invalid.");

        public static readonly Error Inactive =
            Error.Unauthorized("User.Inactive", "The user account has been deactivated.");
    }

    public static class RefreshToken
    {
        public static readonly Error NotFound =
            Error.NotFound("RefreshToken.NotFound", "The refresh token was not found.");

        public static readonly Error Revoked =
            Error.Unauthorized("RefreshToken.Revoked", "The refresh token has already been revoked.");

        public static readonly Error Expired =
            Error.Unauthorized("RefreshToken.Expired", "The refresh token has expired.");

        public static readonly Error Invalid =
            Error.Unauthorized("RefreshToken.Invalid", "The refresh token is invalid.");
    }

    public static class Token
    {
        public static readonly Error Invalid =
            Error.Unauthorized("Token.Invalid", "The provided token is invalid.");
    }
}