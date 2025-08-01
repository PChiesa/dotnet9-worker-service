using MediatR;

namespace WorkerService.Application.Commands;

public record ShipOrderCommand(Guid OrderId, string TrackingNumber) : IRequest<bool>;