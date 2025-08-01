using MediatR;

namespace WorkerService.Application.Commands;

public record CancelOrderCommand(Guid OrderId, string? Reason = null) : IRequest<bool>;