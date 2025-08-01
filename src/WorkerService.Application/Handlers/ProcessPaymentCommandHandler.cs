using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using WorkerService.Application.Commands;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;

namespace WorkerService.Application.Handlers;

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("ProcessPayment");
        activity?.SetTag("order.id", request.OrderId.ToString());
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing payment for order {OrderId}", request.OrderId);

            var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for payment processing", request.OrderId);
                return false;
            }

            // Process payment
            order.ProcessPayment();
            
            await _orderRepository.UpdateAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment processed successfully for order {OrderId}", request.OrderId);

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
                new KeyValuePair<string, object?>("operation", "payment_processed"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", request.OrderId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderUpdateDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}