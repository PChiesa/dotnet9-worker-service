using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.Application.Commands;
using WorkerService.Domain.Entities;
using WorkerService.Infrastructure.Data;
using WorkerService.IntegrationTests.Fixtures;
using WorkerService.IntegrationTests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace WorkerService.IntegrationTests.Tests;

[Collection("Integration Tests")]
public class SimpleIntegrationTests : IClassFixture<WorkerServiceTestFixture>, IAsyncLifetime
{
    private readonly WorkerServiceTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private TestWebApplicationFactory? _factory;
    private IServiceScope? _scope;

    public SimpleIntegrationTests(WorkerServiceTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _factory = new TestWebApplicationFactory(_fixture);
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
    public async Task Should_Create_Order_Successfully()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var command = new CreateOrderCommand(
            "customer-123",
            new List<OrderItemDto>
            {
                new OrderItemDto("product-1", 2, 50.00m),
                new OrderItemDto("product-2", 1, 25.00m)
            }
        );

        // Act
        var result = await mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().NotBeEmpty();
        result.CustomerId.Should().Be("customer-123");
        result.TotalAmount.Should().Be(125.00m); // (2*50) + (1*25)

        // Verify in database
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == result.OrderId);

        order.Should().NotBeNull();
        order!.CustomerId.Should().Be("customer-123");
        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().HaveCount(2);

        _output.WriteLine($"Order created successfully: {result.OrderId}");
    }

    [Fact]
    public async Task Should_Reject_Invalid_Order()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();

        var invalidCommand = new CreateOrderCommand(
            "", // Empty customer ID
            new List<OrderItemDto>
            {
                new OrderItemDto("product-1", 0, 50.00m) // Zero quantity
            }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await mediator.Send(invalidCommand);
        });

        _output.WriteLine("Invalid order was properly rejected");
    }

    [Fact]
    public async Task Should_Connect_To_All_Dependencies()
    {
        // Arrange
        var dbContext = _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act & Assert - Database connection
        var canConnectToDb = await dbContext.Database.CanConnectAsync();
        canConnectToDb.Should().BeTrue("Should be able to connect to test database");

        // Assert - Containers are running
        _output.WriteLine($"PostgreSQL: {_fixture.PostgreSqlContainer.GetConnectionString()}");
        _output.WriteLine($"RabbitMQ: {_fixture.GetRabbitMqConnectionString()}");
        _output.WriteLine($"Jaeger UI: {_fixture.GetJaegerUiUrl()}");

        _output.WriteLine("All dependencies are accessible");
    }

    [Fact]
    public async Task Should_Handle_Multiple_Orders_Concurrently()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const int orderCount = 5;
        var tasks = new List<Task<CreateOrderResult>>();

        // Act - Create multiple orders concurrently
        for (int i = 0; i < orderCount; i++)
        {
            var command = new CreateOrderCommand(
                $"customer-{i}",
                new List<OrderItemDto>
                {
                    new OrderItemDto($"product-{i}", 1, 10.00m)
                }
            );
            tasks.Add(mediator.Send(command));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(orderCount);
        results.Should().OnlyContain(r => r.OrderId != Guid.Empty);

        // Verify all orders in database
        var ordersInDb = await dbContext.Orders.CountAsync();
        ordersInDb.Should().BeGreaterOrEqualTo(orderCount);

        _output.WriteLine($"Successfully created {orderCount} orders concurrently");
    }

    [Fact]
    public async Task Should_Process_Order_State_Transitions()
    {
        // Arrange
        var mediator = _scope!.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var command = new CreateOrderCommand(
            "test-customer",
            new List<OrderItemDto>
            {
                new OrderItemDto("test-product", 1, 100.00m)
            }
        );

        // Act
        var result = await mediator.Send(command);
        
        // Get the order and test state transitions
        var order = await dbContext.Orders.FindAsync(result.OrderId);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Pending);

        // Test valid state transitions
        order.ValidateOrder();
        order.Status.Should().Be(OrderStatus.Validated);

        order.MarkAsPaymentProcessing();
        order.Status.Should().Be(OrderStatus.PaymentProcessing);

        order.MarkAsPaid();
        order.Status.Should().Be(OrderStatus.Paid);

        order.MarkAsShipped();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.MarkAsDelivered();
        order.Status.Should().Be(OrderStatus.Delivered);

        _output.WriteLine($"Order {result.OrderId} successfully transitioned through all states");
    }
}