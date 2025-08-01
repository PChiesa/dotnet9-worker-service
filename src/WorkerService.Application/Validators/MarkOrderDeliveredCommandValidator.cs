using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class MarkOrderDeliveredCommandValidator : AbstractValidator<MarkOrderDeliveredCommand>
{
    public MarkOrderDeliveredCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required");
    }
}