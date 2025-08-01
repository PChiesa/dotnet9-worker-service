using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;
using WorkerService.Domain.Events;

namespace WorkerService.Application.Handlers;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("CreateOrder");
        activity?.SetTag("order.customer_id", request.CustomerId);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

            // Create order entity using manual mapping
            var order = request.ToEntity();
            order.MarkAsCreated();

            // Save to database
            await _orderRepository.AddAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", 
                order.Id, order.CustomerId);

            await _publishEndpoint.Publish(new OrderCreatedEvent(order.Id, order.CustomerId, order.TotalAmount.Amount), cancellationToken);
            _logger.LogDebug("Published domain event {EventType} for order {OrderId}", 
                typeof(OrderCreatedEvent).Name, order.Id);
            
            order.ClearDomainEvents();

            // Record metrics
            OrderApiMetrics.OrdersCreated.Add(1, 
                new KeyValuePair<string, object?>("customer_id", request.CustomerId));

            return order.ToCreateResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for customer {CustomerId}", request.CustomerId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderCreationDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}