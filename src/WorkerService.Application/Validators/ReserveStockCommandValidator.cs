using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero");

        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("Order ID is required")
            .MaximumLength(100).WithMessage("Order ID cannot exceed 100 characters");
    }
}