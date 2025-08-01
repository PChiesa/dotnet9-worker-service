using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Cancel reason cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}