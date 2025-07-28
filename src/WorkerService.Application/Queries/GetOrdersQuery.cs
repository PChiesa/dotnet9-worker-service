using MediatR;
using WorkerService.Application.Common.Extensions;

namespace WorkerService.Application.Queries;

/// <summary>
/// Query to retrieve paginated list of orders with optional filtering
/// </summary>
public record GetOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? CustomerId = null) : IRequest<PagedOrdersResult>;