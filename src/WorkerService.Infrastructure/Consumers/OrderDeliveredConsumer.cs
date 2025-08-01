using MassTransit;
using Microsoft.Extensions.Logging;
using WorkerService.Domain.Events;

namespace WorkerService.Infrastructure.Consumers;

public class OrderDeliveredConsumer : IConsumer<OrderDeliveredEvent>
{
    private readonly ILogger<OrderDeliveredConsumer> _logger;

    public OrderDeliveredConsumer(ILogger<OrderDeliveredConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderDeliveredEvent> context)
    {
        var orderDelivered = context.Message;
        
        _logger.LogInformation("Processing OrderDelivered event for Order {OrderId}, Customer {CustomerId}",
            orderDelivered.OrderId, orderDelivered.CustomerId);

        try
        {
            // This consumer handles delivery-related side effects
            // For example: send delivery confirmation, trigger follow-up actions, etc.
            
            // Add correlation ID for tracing
            _logger.LogInformation("Order {OrderId} delivered successfully to customer {CustomerId}. Event ID: {EventId}",
                orderDelivered.OrderId, orderDelivered.CustomerId, orderDelivered.EventId);

            // TODO: Implement actual side effects:
            // - Send delivery confirmation email
            // - Trigger customer satisfaction survey
            // - Update customer order history
            // - Release any pending payments or deposits
            // - Archive order for reporting

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderDelivered event for Order {OrderId}", orderDelivered.OrderId);
            throw; // MassTransit will handle retry policy
        }
    }
}