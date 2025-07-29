using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Commands;

public record UpdateItemCommand(
    Guid ItemId,
    string Name,
    string Description,
    decimal Price,
    string Category
) : IRequest<ItemDto?>;