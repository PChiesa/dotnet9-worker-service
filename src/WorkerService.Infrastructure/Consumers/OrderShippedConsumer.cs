using MassTransit;
using Microsoft.Extensions.Logging;
using WorkerService.Domain.Events;

namespace WorkerService.Infrastructure.Consumers;

public class OrderShippedConsumer : IConsumer<OrderShippedEvent>
{
    private readonly ILogger<OrderShippedConsumer> _logger;

    public OrderShippedConsumer(ILogger<OrderShippedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderShippedEvent> context)
    {
        var orderShipped = context.Message;
        
        _logger.LogInformation("Processing OrderShipped event for Order {OrderId}, Customer {CustomerId}, Tracking {TrackingNumber}",
            orderShipped.OrderId, orderShipped.CustomerId, orderShipped.TrackingNumber);

        try
        {
            // This consumer handles shipping-related side effects
            // For example: send shipping notification, integrate with logistics providers, etc.
            
            // Add correlation ID for tracing
            _logger.LogInformation("Order {OrderId} shipped successfully with tracking number {TrackingNumber}. Event ID: {EventId}",
                orderShipped.OrderId, orderShipped.TrackingNumber, orderShipped.EventId);

            // TODO: Implement actual side effects:
            // - Send shipping notification email with tracking number
            // - Integrate with shipping provider APIs
            // - Update customer with shipping details
            // - Schedule delivery tracking updates

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderShipped event for Order {OrderId}", orderShipped.OrderId);
            throw; // MassTransit will handle retry policy
        }
    }
}