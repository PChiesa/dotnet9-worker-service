using MassTransit;
using Microsoft.Extensions.Logging;
using WorkerService.Domain.Events;

namespace WorkerService.Infrastructure.Consumers;

public class OrderPaidConsumer : IConsumer<OrderPaidEvent>
{
    private readonly ILogger<OrderPaidConsumer> _logger;

    public OrderPaidConsumer(ILogger<OrderPaidConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPaidEvent> context)
    {
        var orderPaid = context.Message;
        
        _logger.LogInformation("Processing OrderPaid event for Order {OrderId}, Amount {Amount}",
            orderPaid.OrderId, orderPaid.Amount);

        try
        {
            // This consumer handles payment-related side effects
            // For example: send confirmation email, update inventory, record financial transaction, etc.
            
            // Add correlation ID for tracing
            _logger.LogInformation("Order {OrderId} payment of {Amount} processed successfully. Event ID: {EventId}",
                orderPaid.OrderId, orderPaid.Amount, orderPaid.EventId);

            // TODO: Implement actual side effects:
            // - Send payment confirmation email
            // - Update inventory reservation
            // - Record payment transaction
            // - Trigger next workflow steps

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderPaid event for Order {OrderId}", orderPaid.OrderId);
            throw; // MassTransit will handle retry policy
        }
    }
}