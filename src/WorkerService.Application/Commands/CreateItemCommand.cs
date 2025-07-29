using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Commands;

public record CreateItemCommand(
    string SKU,
    string Name,
    string Description,
    decimal Price,
    int InitialStock,
    string Category
) : IRequest<ItemDto>;