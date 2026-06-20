using AuthService.Application.Common.Errors;
using AuthService.Application.Common.Interfaces;
using AuthService.Application.Common.Result;
using MediatR;

namespace AuthService.Application.Features.Auth.GetCurrentUser;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUserResponse>>;

// ── Response ──────────────────────────────────────────────────────────────────

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<CurrentUserResponse>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return AuthErrors.User.NotFound;

        if (!user.IsActive)
            return AuthErrors.User.Inactive;

        return new CurrentUserResponse(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString().ToUpperInvariant(),
            CreatedAt: user.CreatedAt);
    }
}