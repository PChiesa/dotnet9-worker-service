using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Queries;

public record GetItemQuery(Guid ItemId) : IRequest<ItemDto?>;