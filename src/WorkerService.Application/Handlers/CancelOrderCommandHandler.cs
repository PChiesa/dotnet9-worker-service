using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using WorkerService.Application.Commands;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;
using WorkerService.Domain.Events;

namespace WorkerService.Application.Handlers;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("CancelOrder");
        activity?.SetTag("order.id", request.OrderId.ToString());
        activity?.SetTag("order.cancel_reason", request.Reason ?? "No reason provided");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Cancelling order {OrderId} with reason: {Reason}", 
                request.OrderId, request.Reason ?? "No reason provided");

            var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for cancellation", request.OrderId);
                return false;
            }

            // Cancel order with reason
            order.Cancel(request.Reason);
            
            await _orderRepository.UpdateAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} cancelled successfully", request.OrderId);

            // Publish domain events
            await _publishEndpoint.Publish(new OrderCancelledEvent(order.Id, order.CustomerId, request.Reason), cancellationToken);
            _logger.LogDebug("Published domain event {EventType} for order {OrderId}", 
                typeof(OrderCancelledEvent).Name, order.Id);
            
            order.ClearDomainEvents();

            // Record metrics
            OrderApiMetrics.OrdersUpdated.Add(1, 
                new KeyValuePair<string, object?>("order_id", request.OrderId.ToString()),
                new KeyValuePair<string, object?>("operation", "cancelled"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", request.OrderId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderUpdateDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}