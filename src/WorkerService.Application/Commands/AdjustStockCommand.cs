using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Commands;

public record AdjustStockCommand(
    Guid ItemId,
    int NewQuantity,
    string Reason
) : IRequest<ItemDto?>;