using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required");
    }
}