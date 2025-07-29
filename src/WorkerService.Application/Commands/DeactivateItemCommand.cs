using MediatR;

namespace WorkerService.Application.Commands;

public record DeactivateItemCommand(Guid ItemId) : IRequest<bool>;