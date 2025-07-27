using MassTransit;
using Microsoft.Extensions.Logging;
using WorkerService.Domain.Events;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Infrastructure.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IOrderRepository orderRepository,
        ILogger<OrderCreatedConsumer> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var orderCreated = context.Message;
        
        _logger.LogInformation("Processing OrderCreated event for Order {OrderId}, Customer {CustomerId}", 
            orderCreated.OrderId, orderCreated.CustomerId);

        try
        {
            // Retrieve the order to validate it
            var order = await _orderRepository.GetOrderWithItemsAsync(orderCreated.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for validation", orderCreated.OrderId);
                return;
            }

            // Validate the order (business logic)
            if (order.Items.Any() && order.TotalAmount.Amount > 0)
            {
                order.ValidateOrder();
                await _orderRepository.UpdateAsync(order, context.CancellationToken);
                
                _logger.LogInformation("Order {OrderId} validated successfully", orderCreated.OrderId);
                
                // Publish order validated event
                await context.Publish(new OrderValidatedEvent(order.Id, order.CustomerId));
            }
            else
            {
                _logger.LogWarning("Order {OrderId} failed validation - no items or zero amount", orderCreated.OrderId);
                order.Cancel();
                await _orderRepository.UpdateAsync(order, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event for Order {OrderId}", orderCreated.OrderId);
            throw; // MassTransit will handle retry policy
        }
    }
}