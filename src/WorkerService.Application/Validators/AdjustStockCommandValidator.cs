using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required");

        RuleFor(x => x.NewQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason for adjustment is required")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters");
    }
}