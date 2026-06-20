using AuthService.Application.Common.Errors;
using AuthService.Application.Common.Interfaces;
using AuthService.Application.Common.Result;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AuthService.Application.Features.Auth.Register;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName,
    UserRole Role = UserRole.Trader
) : IRequest<Result<RegisterUserResponse>>;

// ── Response ──────────────────────────────────────────────────────────────────

public sealed record RegisterUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role,
    DateTime CreatedAt
);

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MinimumLength(2).WithMessage("Full name must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Role is not valid.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RegisterUserCommandHandler
    : IRequestHandler<RegisterUserCommand, Result<RegisterUserResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPublisher _publisher;

    public RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPublisher publisher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _publisher = publisher;
    }

    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Check for duplicate email
        var exists = await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken);
        if (exists)
            return AuthErrors.User.EmailAlreadyExists;

        // 2. Hash password with bcrypt cost factor 12
        var passwordHash = _passwordHasher.Hash(request.Password);

        // 3. Create aggregate root (raises UserRegisteredDomainEvent internally)
        var user = User.Create(
            email: request.Email,
            passwordHash: passwordHash,
            fullName: request.FullName,
            role: request.Role);

        // 4. Persist
        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // 5. Publish domain events
        foreach (var domainEvent in user.PopDomainEvents())
            await _publisher.Publish(domainEvent, cancellationToken);

        return new RegisterUserResponse(
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            CreatedAt: user.CreatedAt);
    }
}