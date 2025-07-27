using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Worker.Health;

public class WorkerHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerHealthCheck> _logger;

    public WorkerHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkerHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            // Check for orders that have been stuck in processing states for too long
            var stalledOrders = await CheckForStalledOrdersAsync(orderRepository, cancellationToken);
            
            // Check database connectivity by attempting a simple query
            var recentOrders = await orderRepository.GetOrdersCreatedBetweenAsync(
                DateTime.UtcNow.AddHours(-1), 
                DateTime.UtcNow, 
                cancellationToken);

            var recentOrderCount = recentOrders.Count();

            var data = new Dictionary<string, object>
            {
                ["stalled_orders"] = stalledOrders,
                ["recent_orders_count"] = recentOrderCount,
                ["check_time"] = DateTime.UtcNow
            };

            if (stalledOrders > 10)
            {
                _logger.LogWarning("Health check detected {StalledOrders} stalled orders", stalledOrders);
                return HealthCheckResult.Degraded(
                    $"Found {stalledOrders} stalled orders - system may be experiencing issues",
                    data: data);
            }

            if (stalledOrders > 0)
            {
                _logger.LogInformation("Health check detected {StalledOrders} stalled orders", stalledOrders);
                return HealthCheckResult.Healthy(
                    $"System healthy with {stalledOrders} orders requiring attention",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Worker service is healthy - no stalled orders detected",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy(
                "Worker service health check failed",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["check_time"] = DateTime.UtcNow
                });
        }
    }

    private async Task<int> CheckForStalledOrdersAsync(IOrderRepository orderRepository, CancellationToken cancellationToken)
    {
        var stalledCount = 0;
        
        // Check for orders stuck in each processing state
        var pendingOrders = await orderRepository.GetOrdersByStatusAsync(OrderStatus.Pending, cancellationToken);
        stalledCount += pendingOrders.Count(o => DateTime.UtcNow - o.CreatedAt > TimeSpan.FromMinutes(30));

        var paymentProcessingOrders = await orderRepository.GetOrdersByStatusAsync(OrderStatus.PaymentProcessing, cancellationToken);
        stalledCount += paymentProcessingOrders.Count(o => DateTime.UtcNow - o.UpdatedAt > TimeSpan.FromMinutes(10));

        var shippedOrders = await orderRepository.GetOrdersByStatusAsync(OrderStatus.Shipped, cancellationToken);
        stalledCount += shippedOrders.Count(o => DateTime.UtcNow - o.UpdatedAt > TimeSpan.FromDays(7));

        return stalledCount;
    }
}