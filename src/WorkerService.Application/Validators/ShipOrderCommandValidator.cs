using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required");

        RuleFor(x => x.TrackingNumber)
            .NotEmpty()
            .WithMessage("Tracking number is required")
            .MaximumLength(100)
            .WithMessage("Tracking number cannot exceed 100 characters");
    }
}