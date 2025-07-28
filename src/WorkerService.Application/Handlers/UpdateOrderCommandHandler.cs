using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;
using WorkerService.Application.Common.Metrics;

namespace WorkerService.Application.Handlers;

public class UpdateOrderCommandHandler : IRequestHandler<UpdateOrderCommand, UpdateOrderResult?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UpdateOrderCommandHandler> _logger;

    public UpdateOrderCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<UpdateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<UpdateOrderResult?> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("UpdateOrder");
        activity?.SetTag("order.id", request.OrderId.ToString());
        activity?.SetTag("order.customer_id", request.CustomerId);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Updating order {OrderId} for customer {CustomerId}", 
                request.OrderId, request.CustomerId);

            var existingOrder = await _orderRepository.GetOrderWithItemsAsync(request.OrderId, cancellationToken);
            if (existingOrder == null)
            {
                _logger.LogWarning("Order {OrderId} not found for update", request.OrderId);
                return null;
            }

            // Update order properties using domain methods
            existingOrder.UpdateCustomerId(request.CustomerId);
            
            // Clear existing items and add new ones
            existingOrder.ClearItems();
            foreach (var itemDto in request.Items)
            {
                var orderItem = new OrderItem(itemDto.ProductId, itemDto.Quantity, new Money(itemDto.UnitPrice));
                existingOrder.AddItem(orderItem);
            }

            await _orderRepository.UpdateAsync(existingOrder, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} updated for customer {CustomerId}", 
                existingOrder.Id, existingOrder.CustomerId);

            // Publish domain events
            foreach (var domainEvent in existingOrder.DomainEvents)
            {
                await _publishEndpoint.Publish(domainEvent, cancellationToken);
                _logger.LogDebug("Published domain event {EventType} for order {OrderId}", 
                    domainEvent.GetType().Name, existingOrder.Id);
            }
            
            existingOrder.ClearDomainEvents();

            // Record metrics
            OrderApiMetrics.OrdersUpdated.Add(1, 
                new KeyValuePair<string, object?>("customer_id", request.CustomerId));

            return existingOrder.ToUpdateResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update order {OrderId}", request.OrderId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderUpdateDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}