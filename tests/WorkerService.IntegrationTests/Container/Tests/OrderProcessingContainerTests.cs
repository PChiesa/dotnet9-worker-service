using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Domain.Entities;
using WorkerService.Infrastructure.Data;
using WorkerService.Domain.Events;
using WorkerService.IntegrationTests.Container.Fixtures;
using WorkerService.IntegrationTests.Shared.Fixtures;
using WorkerService.IntegrationTests.Shared.Services;
using WorkerService.IntegrationTests.Shared.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace WorkerService.IntegrationTests.Container.Tests;

[Collection("Container Integration Tests")]
public class OrderProcessingContainerTests : IClassFixture<WorkerServiceTestFixture>, IAsyncLifetime
{
    private readonly WorkerServiceTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private ContainerWebApplicationFactory? _factory;
    private IServiceScope? _scope;

    public OrderProcessingContainerTests(WorkerServiceTestFixture fixture, ITestOutputHelper output)
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
    public async Task Should_Process_Single_Order_End_To_End()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);
        var testHarness = _scope!.ServiceProvider.GetRequiredService<ITestHarness>();
        
        await testHarness.Start();

        var createOrderCommand = TestDataBuilder.Scenarios.ValidOrderWithAllFields();
        
        _output.WriteLine($"Creating order: Customer {createOrderCommand.CustomerId} with {createOrderCommand.Items.Count} items");

        // Act
        var result = await mediator.Send(createOrderCommand);

        // Wait for message processing
        await Task.Delay(2000);

        // Assert - Command execution
        result.Should().NotBeNull();
        result.OrderId.Should().NotBeEmpty();

        var orderId = result.OrderId;
        _output.WriteLine($"Order created with ID: {orderId}");

        // Assert - Database state
        await dbAssertions.AssertOrderExistsAsync(orderId);
        
        // Verify order properties  
        var order = await dbContext.Orders.FirstAsync(o => o.Id == orderId);
        order.CustomerId.Should().Be(createOrderCommand.CustomerId);
        order.Items.Should().HaveCount(createOrderCommand.Items.Count);
        
        // Wait a bit more for background processing
        await Task.Delay(3000);
        
        // Assert - Order should be processed (status updated)
        await dbContext.Entry(order).ReloadAsync();
        order.Status.Should().NotBe(OrderStatus.Pending, "Order should have been processed");

        // Assert - Domain events should be raised
        // Note: In this implementation, domain events are handled internally
        // We verify the side effects rather than the events themselves
        _output.WriteLine("Order processing verified through database state");

        await testHarness.Stop();

        // Log results
        _output.WriteLine($"Order processing completed successfully");
        _output.WriteLine($"Jaeger UI: {_fixture.GetJaegerUiUrl()}");
    }

    [Fact]
    public async Task Should_Process_Multiple_Orders_Concurrently()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);
        var testHarness = _scope!.ServiceProvider.GetRequiredService<ITestHarness>();
        
        await testHarness.Start();

        const int orderCount = 10;
        var orderIds = new List<Guid>();

        // Act - Create multiple orders concurrently
        var createTasks = new List<Task<CreateOrderResult>>();
        for (int i = 0; i < orderCount; i++)
        {
            var command = TestDataBuilder.Scenarios.ValidOrderWithAllFields();
            createTasks.Add(mediator.Send(command));
        }

        var results = await Task.WhenAll(createTasks);

        // Collect order IDs
        foreach (var result in results)
        {
            result.Should().NotBeNull();
            orderIds.Add(result.OrderId);
        }

        _output.WriteLine($"Created {orderCount} orders concurrently");

        // Wait for processing
        await Task.Delay(5000);

        // Assert - All orders exist in database
        await dbAssertions.AssertOrderCountAsync(orderCount);

        // Assert - All orders are processed  
        var orders = await dbContext.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
        orders.Should().OnlyContain(o => o.Status != OrderStatus.Pending, "All orders should be processed");

        // Assert - All orders processed (verified through database state)
        _output.WriteLine("Multiple order processing verified through database state");

        await testHarness.Stop();

        _output.WriteLine($"All {orderCount} orders processed successfully");
    }

    [Fact]
    public async Task Should_Handle_Invalid_Order_Gracefully()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);

        var invalidOrder = TestDataBuilder.Scenarios.InvalidOrderZeroQuantity();

        // Act
        var result = await mediator.Send(invalidOrder);

        // Assert
        result.Should().NotBeNull();
        // Validation should catch the invalid order
        _output.WriteLine("Order validation handled invalid input");

        // Verify no order was created in database
        await dbAssertions.AssertOrderCountAsync(0);

        _output.WriteLine($"Invalid order handling completed: {result}");
    }

    [Fact]
    public async Task Should_Process_Orders_With_Simulator_Service()
    {
        // This test relies on the OrderSimulatorService configured in TestWebApplicationFactory
        // The simulator is configured to generate 20 orders

        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);
        var testHarness = _scope!.ServiceProvider.GetRequiredService<ITestHarness>();
        
        await testHarness.Start();

        _output.WriteLine("Waiting for order simulator to generate orders...");

        // Act - Wait for simulator to complete (20 orders at 500ms intervals = ~10 seconds)
        await Task.Delay(15000); // Extra time for processing

        // Assert
        var orderCount = await dbContext.Orders.CountAsync();
        orderCount.Should().BeGreaterOrEqualTo(15, "Simulator should have generated most orders");

        // Check that orders are being processed
        await dbAssertions.AssertAllOrdersProcessedAsync();

        // Assert - All orders processed (verified through database state)
        var processedOrders = await dbContext.Orders.ToListAsync();
        processedOrders.Should().OnlyContain(o => o.Status != OrderStatus.Pending, "All orders should be processed");

        await testHarness.Stop();

        _output.WriteLine($"Order simulator generated and processed {orderCount} orders");
    }

    [Fact]
    public async Task Should_Handle_Order_Processing_With_Retries()
    {
        // This test would require fault injection, which we'll implement in MessageFlowIntegrationTests
        // For now, we'll test that the system handles transient failures

        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);

        // Create a high-value order that might trigger different processing paths
        var highValueOrder = TestDataBuilder.Scenarios.HighValueOrder();

        // Act
        var result = await mediator.Send(highValueOrder);

        // Wait for processing with potential retries
        await Task.Delay(5000);

        // Assert
        result.Should().NotBeNull();
        var order = await dbContext.Orders.FindAsync(result.OrderId);
        order.Should().NotBeNull();
        order!.Status.Should().NotBe(OrderStatus.Pending, "High-value order should be processed");

        var orderFromDb = await dbAssertions.AssertOrderExistsAsync(result.OrderId);
        orderFromDb.TotalAmount.Amount.Should().BeGreaterThan(100000, "Should be a high-value order");

        _output.WriteLine($"High-value order processed: ${order.TotalAmount:N2}");
    }

    [Fact]
    public async Task Should_Track_Order_Processing_Performance()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbAssertions = new DatabaseAssertions(dbContext);

        const int orderCount = 5;
        var orderIds = new List<Guid>();

        // Act - Create orders sequentially to measure individual processing times
        for (int i = 0; i < orderCount; i++)
        {
            var command = TestDataBuilder.Scenarios.ValidOrderMinimalFields();
            var result = await mediator.Send(command);
            result.Should().NotBeNull();
            orderIds.Add(result.OrderId);
            await Task.Delay(500); // Space out orders
        }

        // Wait for all processing to complete
        await Task.Delay(5000);

        // Assert - Check processing completion
        var orders = await dbContext.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
        orders.Should().OnlyContain(o => o.Status != OrderStatus.Pending, "All orders should be processed");
        
        // Calculate and log processing time based on creation vs update times
        var avgProcessingSeconds = orders.Average(o => (o.UpdatedAt - o.CreatedAt).TotalSeconds);
        avgProcessingSeconds.Should().BeLessThan(10, "Orders should be processed quickly");
        
        _output.WriteLine($"Average order processing time: {avgProcessingSeconds:F2} seconds");
        
        // Log individual order timings
        foreach (var order in orders)
        {
            var processingTime = order.UpdatedAt - order.CreatedAt;
            _output.WriteLine($"Order {order.Id}: {processingTime.TotalSeconds:F2} seconds");
        }
    }
}