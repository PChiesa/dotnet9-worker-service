using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Common.Metrics;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class GetItemsQueryHandler : IRequestHandler<GetItemsQuery, PagedItemsResult>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<GetItemsQueryHandler> _logger;

    public GetItemsQueryHandler(IItemRepository repository, ILogger<GetItemsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PagedItemsResult> Handle(GetItemsQuery request, CancellationToken cancellationToken)
    {
        using var activity = ItemApiMetrics.ActivitySource.StartActivity("GetItems");
        activity?.SetTag("page.number", request.PageNumber);
        activity?.SetTag("page.size", request.PageSize);
        activity?.SetTag("filter.category", request.Category ?? "none");
        activity?.SetTag("filter.active", request.IsActive?.ToString() ?? "none");
        activity?.SetTag("search.term", !string.IsNullOrEmpty(request.SearchTerm) ? "provided" : "none");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting items - Page: {Page}, Size: {Size}", 
                request.PageNumber, request.PageSize);

            var (items, totalCount) = await _repository.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.Category,
                request.IsActive,
                request.SearchTerm,
                cancellationToken
            );

            var itemDtos = items.Select(i => i.ToDto());
            var itemsCount = itemDtos.Count();

            // Record metrics
            ItemApiMetrics.ItemsRetrieved.Add(itemsCount);
            ItemApiMetrics.ItemsPerPage.Record(itemsCount);

            return new PagedItemsResult(
                itemDtos,
                totalCount,
                request.PageNumber,
                request.PageSize,
                request.PageNumber * request.PageSize < totalCount,
                request.PageNumber > 1
            );
        }
        finally
        {
            stopwatch.Stop();
            ItemApiMetrics.ItemQueryDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}