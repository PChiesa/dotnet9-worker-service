using MediatR;

namespace WorkerService.Application.Commands;

public record MarkOrderDeliveredCommand(Guid OrderId) : IRequest<bool>;