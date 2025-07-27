using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Worker.Configuration;

namespace WorkerService.Worker.Services;

public class OrderProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderProcessingService> _logger;
    private readonly InMemorySettings _inMemorySettings;

    public OrderProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderProcessingService> logger,
        IOptions<InMemorySettings> inMemorySettings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _inMemorySettings = inMemorySettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessingService starting with configuration: {Configuration}",
            _inMemorySettings.GetConfigurationSummary());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOrdersAsync(stoppingToken);

                // Determine delay based on configuration - faster polling for development
                var delay = _inMemorySettings.HasInMemoryProviders
                    ? TimeSpan.FromSeconds(30) // Faster polling for development
                    : TimeSpan.FromMinutes(1); // Normal production interval

                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("OrderProcessingService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in OrderProcessingService");
                // Back off on error
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("OrderProcessingService stopped");
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        _logger.LogDebug("Checking for pending orders to process");

        var pendingOrders = await orderRepository.GetOrdersByStatusAsync(OrderStatus.Pending, cancellationToken);

        foreach (var order in pendingOrders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Check if order has been pending too long (e.g., 30 minutes)
                if (DateTime.UtcNow - order.CreatedAt > TimeSpan.FromMinutes(30))
                {
                    _logger.LogWarning("Order {OrderId} has been pending for too long, cancelling", order.Id);
                    order.Cancel();
                    await orderRepository.UpdateAsync(order, cancellationToken);
                }
                else
                {
                    _logger.LogDebug("Order {OrderId} is still within processing window", order.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderProcessingService is stopping gracefully");
        await base.StopAsync(cancellationToken);
    }
}