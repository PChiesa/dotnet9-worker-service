using MediatR;
using WorkerService.Application.Common.Extensions;

namespace WorkerService.Application.Queries;

public record GetOrderQuery(Guid OrderId) : IRequest<OrderResponseDto?>;