using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Worker.Services;

public class MetricsCollectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _orderCounter;
    private readonly Histogram<double> _orderProcessingDuration;

    public MetricsCollectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<MetricsCollectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _meter = new Meter("WorkerService.Metrics");
        _orderCounter = _meter.CreateCounter<long>("orders_total", "count", "Total number of orders by status");
        _orderProcessingDuration = _meter.CreateHistogram<double>("order_processing_duration", "seconds", "Order processing duration");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricsCollectionService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectOrderMetricsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MetricsCollectionService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in MetricsCollectionService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("MetricsCollectionService stopped");
    }

    private async Task CollectOrderMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        _logger.LogDebug("Collecting order metrics");

        try
        {
            // Collect metrics for each order status
            foreach (OrderStatus status in Enum.GetValues<OrderStatus>())
            {
                var orders = await orderRepository.GetOrdersByStatusAsync(status, cancellationToken);
                var count = orders.Count();
                
                _orderCounter.Add(count, new KeyValuePair<string, object?>("status", status.ToString()));
                
                _logger.LogDebug("Found {Count} orders with status {Status}", count, status);
            }

            // Calculate average processing duration for completed orders
            var completedOrders = await orderRepository.GetOrdersByStatusAsync(OrderStatus.Delivered, cancellationToken);
            foreach (var order in completedOrders.Where(o => o.UpdatedAt.Date == DateTime.UtcNow.Date))
            {
                var processingDuration = (order.UpdatedAt - order.CreatedAt).TotalSeconds;
                _orderProcessingDuration.Record(processingDuration, 
                    new KeyValuePair<string, object?>("customer_id", order.CustomerId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting order metrics");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MetricsCollectionService is stopping gracefully");
        _meter.Dispose();
        await base.StopAsync(cancellationToken);
    }
}