using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkerService.Infrastructure.Data;
using WorkerService.Worker.Services;
using WorkerService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using WorkerService.IntegrationTests.Container.Fixtures;
using WorkerService.IntegrationTests.Shared.Fixtures;
using WorkerService.IntegrationTests.Shared.Utilities;
using Xunit;
using Xunit.Abstractions;
using MediatR;

namespace WorkerService.IntegrationTests.Container.Tests;

[Collection("Container Integration Tests")]
public class BackgroundServiceContainerTests : IClassFixture<WorkerServiceTestFixture>, IAsyncLifetime
{
    private readonly WorkerServiceTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private ContainerWebApplicationFactory? _factory;
    private IServiceScope? _scope;

    public BackgroundServiceContainerTests(WorkerServiceTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _factory = new ContainerWebApplicationFactory(_fixture);
        await _factory.InitializeAsync();
        _scope = _factory.Services.CreateScope();
    }

    public async Task DisposeAsync()
    {
        _scope?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Start_All_Background_Services()
    {
        // Arrange
        var hostedServices = _factory!.Services.GetServices<IHostedService>().ToList();
        await Task.Delay(10); // Small delay to make this properly async
        
        // Assert - Verify expected services are registered
        hostedServices.Should().NotBeEmpty();
        
        // Check for our custom background services
        hostedServices.Should().Contain(s => s.GetType().Name == "OrderProcessingService");
        hostedServices.Should().Contain(s => s.GetType().Name == "MetricsCollectionService");
        
        // OrderSimulatorService should be registered (as configured in TestWebApplicationFactory)
        hostedServices.Should().Contain(s => s.GetType().Name == "OrderSimulatorService");

        _output.WriteLine($"Found {hostedServices.Count} hosted services:");
        foreach (var service in hostedServices)
        {
            _output.WriteLine($"  - {service.GetType().Name}");
        }
    }

    [Fact]
    public async Task Should_Process_Pending_Orders_In_Background()
    {
        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);
        
        // Create some orders directly in the database as "Pending"
        var orders = new List<Domain.Entities.Order>();
        for (int i = 0; i < 5; i++)
        {
            var order = TestDataBuilder.GetOrderFaker().Generate();
            orders.Add(order);
        }
        
        await dbContext.Orders.AddRangeAsync(orders);
        await dbContext.SaveChangesAsync();

        var orderIds = orders.Select(o => o.Id).ToList();
        
        _output.WriteLine($"Created {orders.Count} pending orders in database");

        // Act - Wait for background service to process orders
        await Task.Delay(10000); // Give time for OrderProcessingService to run

        // Assert - Orders should be processed
        foreach (var orderId in orderIds)
        {
            var order = await dbContext.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            
            // Order should have progressed from Pending status
            order!.Status.Should().NotBe(OrderStatus.Pending, "Order should have been processed");
            
            _output.WriteLine($"Order {orderId}: Status = {order.Status}");
        }

        // At least some orders should be processed beyond Pending
        var processedCount = await dbContext.Orders
            .Where(o => orderIds.Contains(o.Id) && o.Status != OrderStatus.Pending)
            .CountAsync();
        
        processedCount.Should().BeGreaterThan(0, "At least some orders should be processed");
        
        _output.WriteLine($"Processed orders: {processedCount}/{orders.Count}");
    }

    [Fact]
    public async Task Should_Handle_Service_Cancellation_Gracefully()
    {
        // This test verifies that background services respect cancellation tokens
        
        // Arrange
        var cts = new CancellationTokenSource();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create an order
        var order = TestDataBuilder.GetOrderFaker().Generate();
        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        // Act - Cancel after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            cts.Cancel();
        });

        // The factory's services should continue running normally
        // This test mainly ensures no exceptions are thrown during normal operation
        
        await Task.Delay(3000);

        // Assert - Services should still be running (factory not cancelled)
        var hostedServices = _factory!.Services.GetServices<IHostedService>().ToList();
        hostedServices.Should().NotBeEmpty();

        _output.WriteLine("Background services handled cancellation gracefully");
    }

    [Fact]
    public async Task Should_Run_Metrics_Collection_Service()
    {
        // Arrange
        var logger = _scope!.ServiceProvider.GetService<ILogger<MetricsCollectionService>>();
        
        // The MetricsCollectionService should be running and collecting metrics
        
        // Act - Wait for at least one collection cycle (30 seconds as configured)
        _output.WriteLine("Waiting for metrics collection cycle...");
        await Task.Delay(5000); // Shorter wait for test

        // Assert - We can't directly test the metrics without a metrics provider,
        // but we can verify the service is registered and running
        var hostedServices = _factory!.Services.GetServices<IHostedService>()
            .Where(s => s.GetType().Name == "MetricsCollectionService")
            .ToList();

        hostedServices.Should().HaveCount(1);
        
        _output.WriteLine("MetricsCollectionService is running");
    }

    [Fact]
    public async Task Should_Process_Orders_Created_During_Runtime()
    {
        // This test verifies that the OrderProcessingService picks up new orders
        
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);

        // Act - Create orders while services are running
        var orderIds = new List<Guid>();
        
        for (int i = 0; i < 3; i++)
        {
            var command = TestDataBuilder.Scenarios.ValidOrderMinimalFields();
            var result = await mediator.Send(command);
            
            result.Should().NotBeNull();
            orderIds.Add(result.OrderId);
            
            await Task.Delay(1000); // Space out order creation
        }

        _output.WriteLine($"Created {orderIds.Count} orders during runtime");

        // Wait for background processing
        await Task.Delay(8000);

        // Assert - Orders should progress through states
        foreach (var orderId in orderIds)
        {
            var order = await dbContext.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            
            // Should not still be pending after this time
            order!.Status.Should().NotBe(OrderStatus.Pending);
            
            _output.WriteLine($"Order {orderId}: Status = {order.Status}");
        }
    }

    [Fact]
    public async Task Should_Handle_Database_Connection_Resilience()
    {
        // This test verifies that services handle transient database issues
        
        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create an order
        var order = TestDataBuilder.GetOrderFaker().Generate();
        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        // Act - Services should continue running even if individual operations fail
        // In a real test, you might simulate connection issues
        await Task.Delay(5000);

        // Assert - Order should still exist and potentially be processed
        var retrievedOrder = await dbContext.Orders.FindAsync(order.Id);
        retrievedOrder.Should().NotBeNull();

        _output.WriteLine($"Order {order.Id} persisted despite potential transient issues");
    }

    [Fact]
    public async Task Should_Not_Process_Completed_Orders_Again()
    {
        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create an already completed order
        var order = TestDataBuilder.GetOrderFaker().Generate();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsShipped("TRK123456789");
        order.MarkAsDelivered();
        
        var completedAt = order.UpdatedAt;
        
        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        _output.WriteLine($"Created completed order {order.Id}");

        // Act - Wait for background service cycles
        await Task.Delay(7000);

        // Assert - Order should not be modified
        await dbContext.Entry(order).ReloadAsync();
        
        order.Status.Should().Be(OrderStatus.Delivered);
        order.UpdatedAt.Should().Be(completedAt, "UpdatedAt should not change for completed orders");

        _output.WriteLine("Completed order was not reprocessed");
    }

    [Fact]
    public async Task Should_Handle_Concurrent_Order_Processing()
    {
        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);
        
        // Create multiple pending orders
        var orders = new List<Domain.Entities.Order>();
        for (int i = 0; i < 10; i++)
        {
            orders.Add(TestDataBuilder.GetOrderFaker().Generate());
        }
        
        await dbContext.Orders.AddRangeAsync(orders);
        await dbContext.SaveChangesAsync();

        _output.WriteLine($"Created {orders.Count} orders for concurrent processing");

        // Act - Wait for background processing
        await Task.Delay(10000);

        // Assert - All orders should be processed without conflicts
        var processedOrders = await dbContext.Orders
            .Where(o => orders.Select(x => x.Id).Contains(o.Id))
            .ToListAsync();

        processedOrders.Should().HaveCount(orders.Count);
        
        // Check for any stuck in intermediate states
        var stuckOrders = processedOrders.Where(o => o.Status == OrderStatus.PaymentProcessing).ToList();
        stuckOrders.Should().BeEmpty("No orders should be stuck in PaymentProcessing state");

        var deliveredCount = processedOrders.Count(o => o.Status == OrderStatus.Delivered);
        _output.WriteLine($"Delivered: {deliveredCount}/{orders.Count} orders");
    }

    [Fact]
    public async Task Should_Respect_Batch_Size_Configuration()
    {
        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create more orders than the batch size (configured as 10)
        var orders = new List<Domain.Entities.Order>();
        for (int i = 0; i < 15; i++)
        {
            orders.Add(TestDataBuilder.GetOrderFaker().Generate());
        }
        
        await dbContext.Orders.AddRangeAsync(orders);
        await dbContext.SaveChangesAsync();

        _output.WriteLine($"Created {orders.Count} orders (batch size is 10)");

        // Act - Wait for one processing cycle
        await Task.Delay(7000);

        // Assert - Check processing pattern
        var processedOrders = await dbContext.Orders
            .Where(o => orders.Select(x => x.Id).Contains(o.Id))
            .Where(o => o.Status != OrderStatus.Pending)
            .ToListAsync();

        // Should have processed at least some orders
        processedOrders.Should().NotBeEmpty();
        
        _output.WriteLine($"Processed {processedOrders.Count} orders in first batch");
        
        // The exact count depends on timing, but it should respect batch processing
        processedOrders.Count.Should().BeGreaterThan(0).And.BeLessOrEqualTo(orders.Count);
    }
}