using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Queries;

public record GetItemBySkuQuery(string SKU) : IRequest<ItemDto?>;