using Bogus;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;

namespace WorkerService.IntegrationTests.Shared.Services;

public class OrderSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderSimulatorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Faker<CreateOrderCommand> _orderFaker;
    
    public int OrdersGenerated { get; private set; }
    public List<Guid> GeneratedOrderIds { get; } = new();

    public OrderSimulatorService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderSimulatorService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;

        // Configure Bogus faker for realistic order data
        _orderFaker = new Faker<CreateOrderCommand>()
            .CustomInstantiator(f => new CreateOrderCommand(
                f.Random.Guid().ToString(), // CustomerId
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        f.Random.Guid().ToString(), // ProductId
                        f.Random.Int(1, 10), // Quantity
                        Math.Round(f.Random.Decimal(10, 1000), 2) // UnitPrice
                    )
                }
            ));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if order simulation is enabled
        if (!_configuration.GetValue<bool>("OrderSimulator:Enabled"))
        {
            _logger.LogInformation("Order Simulator is disabled");
            return;
        }

        var intervalMs = _configuration.GetValue<int>("OrderSimulator:IntervalMs", 1000);
        var maxOrders = _configuration.GetValue<int>("OrderSimulator:MaxOrders", 10);
        var batchSize = _configuration.GetValue<int>("OrderSimulator:BatchSize", 1);

        _logger.LogInformation(
            "Order Simulator starting - will generate {MaxOrders} orders every {IntervalMs}ms in batches of {BatchSize}",
            maxOrders, intervalMs, batchSize);

        // Wait a bit for services to fully start
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested && OrdersGenerated < maxOrders)
        {
            try
            {
                var ordersToGenerate = Math.Min(batchSize, maxOrders - OrdersGenerated);
                var generateTasks = new List<Task>();

                for (int i = 0; i < ordersToGenerate; i++)
                {
                    generateTasks.Add(GenerateOrderAsync(stoppingToken));
                }

                await Task.WhenAll(generateTasks);

                if (OrdersGenerated < maxOrders)
                {
                    await Task.Delay(intervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in order generation loop");
                await Task.Delay(5000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation(
            "Order Simulator completed - generated {OrdersGenerated} orders",
            OrdersGenerated);
    }

    private async Task GenerateOrderAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var order = _orderFaker.Generate();
            
            _logger.LogDebug(
                "Generating order {OrderNumber}: Customer {CustomerId} - {ItemCount} items",
                OrdersGenerated + 1, order.CustomerId, order.Items.Count);

            var result = await mediator.Send(order, cancellationToken);

            if (result != null)
            {
                OrdersGenerated++;
                GeneratedOrderIds.Add(result.OrderId);
                
                _logger.LogInformation(
                    "Generated order {OrderId} ({OrderNumber}/{MaxOrders}): Customer {CustomerId}",
                    result.OrderId, OrdersGenerated, _configuration.GetValue<int>("OrderSimulator:MaxOrders"),
                    order.CustomerId);
            }
            else
            {
                _logger.LogWarning("Failed to generate order - result was null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating order {OrderNumber}", OrdersGenerated + 1);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Order Simulator stopping - generated {OrdersGenerated} orders total",
            OrdersGenerated);
        
        await base.StopAsync(cancellationToken);
    }
}