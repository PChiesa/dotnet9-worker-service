using MediatR;
using WorkerService.Application.Common.Extensions;

namespace WorkerService.Application.Commands;

/// <summary>
/// Command to update an existing order
/// </summary>
public record UpdateOrderCommand(
    Guid OrderId,
    string CustomerId,
    IList<OrderItemDto> Items) : IRequest<UpdateOrderResult?>;