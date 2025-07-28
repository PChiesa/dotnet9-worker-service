using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;

namespace WorkerService.Application.Handlers;

public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderResponseDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<GetOrderQueryHandler> _logger;

    public GetOrderQueryHandler(
        IOrderRepository orderRepository,
        ILogger<GetOrderQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<OrderResponseDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("GetOrder");
        activity?.SetTag("order.id", request.OrderId.ToString());
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Retrieving order {OrderId}", request.OrderId);

            var order = await _orderRepository.GetOrderWithItemsAsync(request.OrderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", request.OrderId);
                return null;
            }

            _logger.LogInformation("Order {OrderId} retrieved successfully", request.OrderId);

            // Record metrics
            OrderApiMetrics.OrdersRetrieved.Add(1, 
                new KeyValuePair<string, object?>("customer_id", order.CustomerId));

            return order.ToResponseDto();
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderQueryDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}