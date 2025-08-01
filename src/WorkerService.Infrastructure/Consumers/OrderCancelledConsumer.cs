using MassTransit;
using Microsoft.Extensions.Logging;
using WorkerService.Domain.Events;

namespace WorkerService.Infrastructure.Consumers;

public class OrderCancelledConsumer : IConsumer<OrderCancelledEvent>
{
    private readonly ILogger<OrderCancelledConsumer> _logger;

    public OrderCancelledConsumer(ILogger<OrderCancelledConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var orderCancelled = context.Message;
        
        _logger.LogInformation("Processing OrderCancelled event for Order {OrderId}, Customer {CustomerId}, Reason: {Reason}",
            orderCancelled.OrderId, orderCancelled.CustomerId, orderCancelled.Reason ?? "No reason provided");

        try
        {
            // This consumer handles cancellation-related side effects
            // For example: process refunds, release inventory, send notifications, etc.
            
            // Add correlation ID for tracing
            _logger.LogInformation("Order {OrderId} cancelled successfully for customer {CustomerId}. Event ID: {EventId}",
                orderCancelled.OrderId, orderCancelled.CustomerId, orderCancelled.EventId);

            // TODO: Implement actual side effects:
            // - Process refund if payment was already made
            // - Release reserved inventory
            // - Send cancellation notification email
            // - Update analytics/reporting
            // - Handle any third-party service cancellations

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCancelled event for Order {OrderId}", orderCancelled.OrderId);
            throw; // MassTransit will handle retry policy
        }
    }
}