using MediatR;

namespace WorkerService.Application.Commands;

public record ProcessPaymentCommand(Guid OrderId) : IRequest<bool>;