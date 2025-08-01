using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using WorkerService.Application.Commands;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;
using WorkerService.Domain.Events;

namespace WorkerService.Application.Handlers;

public class MarkOrderDeliveredCommandHandler : IRequestHandler<MarkOrderDeliveredCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MarkOrderDeliveredCommandHandler> _logger;

    public MarkOrderDeliveredCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<MarkOrderDeliveredCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(MarkOrderDeliveredCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("MarkOrderDelivered");
        activity?.SetTag("order.id", request.OrderId.ToString());
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Marking order {OrderId} as delivered", request.OrderId);

            var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for delivery marking", request.OrderId);
                return false;
            }

            // Mark as delivered
            order.MarkAsDelivered();
            
            await _orderRepository.UpdateAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} marked as delivered successfully", request.OrderId);

            // Publish domain events
            await _publishEndpoint.Publish(new OrderDeliveredEvent(order.Id, order.CustomerId), cancellationToken);
            _logger.LogDebug("Published domain event {EventType} for order {OrderId}", 
                typeof(OrderDeliveredEvent).Name, order.Id);
            
            order.ClearDomainEvents();

            // Record metrics
            OrderApiMetrics.OrdersUpdated.Add(1, 
                new KeyValuePair<string, object?>("order_id", request.OrderId.ToString()),
                new KeyValuePair<string, object?>("operation", "delivered"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark order {OrderId} as delivered", request.OrderId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderUpdateDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}