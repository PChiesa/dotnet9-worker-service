using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using WorkerService.Application.Commands;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;

namespace WorkerService.Application.Handlers;

public class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ShipOrderCommandHandler> _logger;

    public ShipOrderCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<ShipOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("ShipOrder");
        activity?.SetTag("order.id", request.OrderId.ToString());
        activity?.SetTag("order.tracking_number", request.TrackingNumber);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Shipping order {OrderId} with tracking number {TrackingNumber}", 
                request.OrderId, request.TrackingNumber);

            var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for shipping", request.OrderId);
                return false;
            }

            // Ship order with tracking number
            order.MarkAsShipped(request.TrackingNumber);
            
            await _orderRepository.UpdateAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} shipped successfully with tracking number {TrackingNumber}", 
                request.OrderId, request.TrackingNumber);

            // Publish domain events
            foreach (var domainEvent in order.DomainEvents)
            {
                await _publishEndpoint.Publish(domainEvent, cancellationToken);
                _logger.LogDebug("Published domain event {EventType} for order {OrderId}", 
                    domainEvent.GetType().Name, order.Id);
            }
            
            order.ClearDomainEvents();

            // Record metrics
            OrderApiMetrics.OrdersUpdated.Add(1, 
                new KeyValuePair<string, object?>("order_id", request.OrderId.ToString()),
                new KeyValuePair<string, object?>("operation", "shipped"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ship order {OrderId}", request.OrderId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderUpdateDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}