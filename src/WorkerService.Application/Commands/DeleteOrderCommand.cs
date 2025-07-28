using MediatR;

namespace WorkerService.Application.Commands;

/// <summary>
/// Command to soft delete an order by marking it as deleted
/// </summary>
public record DeleteOrderCommand(Guid OrderId) : IRequest<bool>;