using FluentValidation;
using TradingService.Domain.Enums;

namespace TradingService.Application.Features.Orders.Commands.SubmitOrder;

public sealed class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
{
    public SubmitOrderCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required.")
            .MaximumLength(20)
            .WithMessage("Symbol must not exceed 20 characters.")
            .Matches("^[A-Za-z]+$")
            .WithMessage("Symbol must contain only letters.");

        RuleFor(x => x.Side)
            .IsInEnum()
            .WithMessage("Side must be either Buy or Sell.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be either Limit or Market.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.")
            .LessThanOrEqualTo(1_000_000)
            .WithMessage("Quantity must not exceed 1,000,000.");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("IdempotencyKey is required.")
            .MaximumLength(100)
            .WithMessage("IdempotencyKey must not exceed 100 characters.");
    }
}
