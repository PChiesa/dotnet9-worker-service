using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;

namespace WorkerService.Application.Handlers;

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, PagedOrdersResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<GetOrdersQueryHandler> _logger;

    public GetOrdersQueryHandler(
        IOrderRepository orderRepository,
        ILogger<GetOrdersQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<PagedOrdersResult> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("GetOrders");
        activity?.SetTag("page.number", request.PageNumber);
        activity?.SetTag("page.size", request.PageSize);
        activity?.SetTag("customer.id", request.CustomerId ?? "all");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Retrieving orders - Page: {PageNumber}, Size: {PageSize}, Customer: {CustomerId}", 
                request.PageNumber, request.PageSize, request.CustomerId ?? "all");

            var pagedData = await _orderRepository.GetPagedAsync(
                request.PageNumber, request.PageSize, request.CustomerId, cancellationToken);
            
            _logger.LogDebug("Retrieved {Count} orders for page {PageNumber}", 
                pagedData.Orders.Count(), request.PageNumber);

            // Record metrics
            OrderApiMetrics.OrdersRetrieved.Add(pagedData.Orders.Count(), 
                new KeyValuePair<string, object?>("operation", "list"));

            return pagedData.ToPagedResult(request.PageNumber, request.PageSize);
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderQueryDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}