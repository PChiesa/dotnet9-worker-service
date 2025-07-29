using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Queries;

public record GetItemsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Category = null,
    bool? IsActive = null,
    string? SearchTerm = null
) : IRequest<PagedItemsResult>;