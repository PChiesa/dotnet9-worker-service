using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Commands;

public record ReserveStockCommand(
    Guid ItemId,
    int Quantity,
    string OrderId
) : IRequest<ItemDto?>;