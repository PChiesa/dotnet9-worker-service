using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Domain.Interfaces;
using WorkerService.Application.Common.Metrics;

namespace WorkerService.Application.Handlers;

public class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<DeleteOrderCommandHandler> _logger;

    public DeleteOrderCommandHandler(
        IOrderRepository orderRepository,
        ILogger<DeleteOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        using var activity = OrderApiMetrics.ActivitySource.StartActivity("DeleteOrder");
        activity?.SetTag("order.id", request.OrderId.ToString());
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Deleting order {OrderId}", request.OrderId);

            var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for deletion", request.OrderId);
                return false;
            }

            // Soft delete by updating status
            order.MarkAsDeleted();
            
            await _orderRepository.UpdateAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} marked as deleted", request.OrderId);

            // Record metrics
            OrderApiMetrics.OrdersDeleted.Add(1, 
                new KeyValuePair<string, object?>("order_id", request.OrderId.ToString()));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete order {OrderId}", request.OrderId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            OrderApiMetrics.OrderUpdateDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}