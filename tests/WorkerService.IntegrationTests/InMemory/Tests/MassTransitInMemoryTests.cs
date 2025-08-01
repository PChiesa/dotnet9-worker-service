using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.Application.Commands;
using WorkerService.Domain.Events;
using WorkerService.Infrastructure.Consumers;
using WorkerService.Infrastructure.Data;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using WorkerService.IntegrationTests.Shared.Utilities;
using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Tests;

[Collection("InMemory Integration Tests")]
public class MassTransitInMemoryTests : IClassFixture<InMemoryWebApplicationFactory>, IAsyncDisposable
{
    private readonly InMemoryWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private readonly ITestHarness _harness;

    public MassTransitInMemoryTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
        // Get the test harness from the service provider directly (it's a singleton)
        _harness = _factory.Services.GetRequiredService<ITestHarness>();
        _client = _factory.CreateClient();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // Start the test harness
        _harness.Start().GetAwaiter().GetResult();
    }

    #region Event Publishing Integration Tests

    [Fact]
    public async Task ProcessPayment_ShouldPublishOrderPaidEvent()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        
        var order = await CreateValidatedTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);

        // Act
        var response = await authenticatedClient.PostAsync($"/api/orders/{order.OrderId}/pay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for messages to be processed
        await Task.Delay(1000);

        // Let's check if any messages were published at all
        var allPublished = _harness.Published.Select<OrderPaidEvent>().ToList();
        
        // Verify that OrderPaidEvent was published        
        (await _harness.Published.Any<OrderPaidEvent>()).Should().BeTrue($"Expected OrderPaidEvent to be published for order {order.OrderId}, but found {allPublished.Count} messages");

        var publishedEvent = _harness.Published.Select<OrderPaidEvent>().FirstOrDefault();
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        publishedEvent.Context.Message.Amount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task ShipOrder_ShouldPublishOrderShippedEvent()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreatePaidTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);
        var trackingNumber = "TRACK123456789";
        
        var shipRequest = new { TrackingNumber = trackingNumber };

        // Act
        var response = await authenticatedClient.PostAsJsonAsync($"/api/orders/{order.OrderId}/ship", shipRequest, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that OrderShippedEvent was published
        (await _harness.Published.Any<OrderShippedEvent>()).Should().BeTrue();
        
        var publishedEvent = _harness.Published.Select<OrderShippedEvent>().FirstOrDefault();
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        publishedEvent.Context.Message.CustomerId.Should().Be(order.CustomerId);
        publishedEvent.Context.Message.TrackingNumber.Should().Be(trackingNumber);
    }

    [Fact]
    public async Task MarkAsDelivered_ShouldPublishOrderDeliveredEvent()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreateShippedTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);


        // Act
        var response = await authenticatedClient.PostAsync($"/api/orders/{order.OrderId}/deliver", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that OrderDeliveredEvent was published
        (await _harness.Published.Any<OrderDeliveredEvent>()).Should().BeTrue();
        
        var publishedEvent = _harness.Published.Select<OrderDeliveredEvent>().FirstOrDefault();
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        publishedEvent.Context.Message.CustomerId.Should().Be(order.CustomerId);
    }

    [Fact]
    public async Task CancelOrder_ShouldPublishOrderCancelledEvent()
    {
        // Arrange  
        await _factory.ClearDatabaseAsync();
        var order = await CreateTestOrderAsync(_client);
        var authenticatedClient = GetAuthenticatedClient(_client);
        var cancellationReason = "Customer changed mind";


        var cancelRequest = new { Reason = cancellationReason };

        // Act
        var response = await authenticatedClient.PostAsJsonAsync($"/api/orders/{order.OrderId}/cancel", cancelRequest, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that OrderCancelledEvent was published
        (await _harness.Published.Any<OrderCancelledEvent>()).Should().BeTrue();
        
        var publishedEvent = _harness.Published.Select<OrderCancelledEvent>().FirstOrDefault();
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        publishedEvent.Context.Message.CustomerId.Should().Be(order.CustomerId);
        publishedEvent.Context.Message.Reason.Should().Be(cancellationReason);
    }

    [Fact]
    public async Task CreateOrder_ShouldPublishOrderCreatedEvent()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createCommand = OrderTestData.SimpleCreateCommand();
        var authenticatedClient = GetAuthenticatedClient(_client);


        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", createCommand, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        result.Should().NotBeNull();

        // Verify that OrderCreatedEvent was published
        (await _harness.Published.Any<OrderCreatedEvent>()).Should().BeTrue();
        
        var publishedEvent = _harness.Published.Select<OrderCreatedEvent>().FirstOrDefault();
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Context.Message.OrderId.Should().Be(result!.OrderId);
        publishedEvent.Context.Message.CustomerId.Should().Be(createCommand.CustomerId);
        publishedEvent.Context.Message.TotalAmount.Should().Be(result.TotalAmount);
    }

    #endregion

    #region Consumer Integration Tests

    [Fact]
    public async Task OrderPaidEvent_ShouldBeConsumedByOrderPaidConsumer()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreateValidatedTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);


        // Act - Trigger payment processing which should publish OrderPaidEvent
        var response = await authenticatedClient.PostAsync($"/api/orders/{order.OrderId}/pay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that the event was consumed by OrderPaidConsumer
        (await _harness.Consumed.Any<OrderPaidEvent>()).Should().BeTrue();
        
        var consumedEvent = _harness.Consumed.Select<OrderPaidEvent>().FirstOrDefault();
        consumedEvent.Should().NotBeNull();
        consumedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        consumedEvent.Context.Message.Amount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task OrderShippedEvent_ShouldBeConsumedByOrderShippedConsumer()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreatePaidTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);
        var trackingNumber = "TRACK123456789";

        
        var shipRequest = new { TrackingNumber = trackingNumber };

        // Act - Trigger shipping which should publish OrderShippedEvent
        var response = await authenticatedClient.PostAsJsonAsync($"/api/orders/{order.OrderId}/ship", shipRequest, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that the event was consumed by OrderShippedConsumer
        (await _harness.Consumed.Any<OrderShippedEvent>()).Should().BeTrue();
        
        var consumedEvent = _harness.Consumed.Select<OrderShippedEvent>().FirstOrDefault();
        consumedEvent.Should().NotBeNull();
        consumedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        consumedEvent.Context.Message.CustomerId.Should().Be(order.CustomerId);
        consumedEvent.Context.Message.TrackingNumber.Should().Be(trackingNumber);
    }

    [Fact]
    public async Task OrderDeliveredEvent_ShouldBeConsumedByOrderDeliveredConsumer()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreateShippedTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);


        // Act - Trigger delivery marking which should publish OrderDeliveredEvent
        var response = await authenticatedClient.PostAsync($"/api/orders/{order.OrderId}/deliver", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that the event was consumed by OrderDeliveredConsumer
        (await _harness.Consumed.Any<OrderDeliveredEvent>()).Should().BeTrue();
        
        var consumedEvent = _harness.Consumed.Select<OrderDeliveredEvent>().FirstOrDefault();
        consumedEvent.Should().NotBeNull();
        consumedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        consumedEvent.Context.Message.CustomerId.Should().Be(order.CustomerId);
    }

    [Fact]
    public async Task OrderCancelledEvent_ShouldBeConsumedByOrderCancelledConsumer()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreateTestOrderAsync(_client);
        var authenticatedClient = GetAuthenticatedClient(_client);
        var cancellationReason = "Customer changed mind";


        var cancelRequest = new { Reason = cancellationReason };

        // Act - Trigger cancellation which should publish OrderCancelledEvent
        var response = await authenticatedClient.PostAsJsonAsync($"/api/orders/{order.OrderId}/cancel", cancelRequest, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that the event was consumed by OrderCancelledConsumer
        (await _harness.Consumed.Any<OrderCancelledEvent>()).Should().BeTrue();
        
        var consumedEvent = _harness.Consumed.Select<OrderCancelledEvent>().FirstOrDefault();
        consumedEvent.Should().NotBeNull();
        consumedEvent!.Context.Message.OrderId.Should().Be(order.OrderId);
        consumedEvent.Context.Message.CustomerId.Should().Be(order.CustomerId);
        consumedEvent.Context.Message.Reason.Should().Be(cancellationReason);
    }

    #endregion

    #region End-to-End Event Flow Tests

    [Fact]
    public async Task CompleteOrderLifecycle_ShouldPublishAllExpectedEvents()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createCommand = OrderTestData.SimpleCreateCommand();
        var authenticatedClient = GetAuthenticatedClient(_client);


        // Act - Execute complete order lifecycle
        
        // 1. Create Order
        var createResponse = await authenticatedClient.PostAsJsonAsync("/api/orders", createCommand, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        var orderId = createResult!.OrderId;

        // 2. Process Payment
        var payResponse = await authenticatedClient.PostAsync($"/api/orders/{orderId}/pay", null);
        payResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Ship Order
        var shipRequest = new { TrackingNumber = "TRACK123456789" };
        var shipResponse = await authenticatedClient.PostAsJsonAsync($"/api/orders/{orderId}/ship", shipRequest, _jsonOptions);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Mark as Delivered
        var deliverResponse = await authenticatedClient.PostAsync($"/api/orders/{orderId}/deliver", null);
        deliverResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Verify all events were published and consumed
        (await _harness.Published.Any<OrderCreatedEvent>()).Should().BeTrue();
        (await _harness.Published.Any<OrderPaidEvent>()).Should().BeTrue();
        (await _harness.Published.Any<OrderShippedEvent>()).Should().BeTrue();
        (await _harness.Published.Any<OrderDeliveredEvent>()).Should().BeTrue();

        (await _harness.Consumed.Any<OrderCreatedEvent>()).Should().BeTrue();
        (await _harness.Consumed.Any<OrderPaidEvent>()).Should().BeTrue();
        (await _harness.Consumed.Any<OrderShippedEvent>()).Should().BeTrue();
        (await _harness.Consumed.Any<OrderDeliveredEvent>()).Should().BeTrue();

        // Verify event data integrity
        var createdEvent = _harness.Published.Select<OrderCreatedEvent>().FirstOrDefault();
        createdEvent!.Context.Message.OrderId.Should().Be(orderId);
        createdEvent.Context.Message.CustomerId.Should().Be(createCommand.CustomerId);

        var paidEvent = _harness.Published.Select<OrderPaidEvent>().FirstOrDefault();
        paidEvent!.Context.Message.OrderId.Should().Be(orderId);

        var shippedEvent = _harness.Published.Select<OrderShippedEvent>().FirstOrDefault();
        shippedEvent!.Context.Message.OrderId.Should().Be(orderId);
        shippedEvent.Context.Message.TrackingNumber.Should().Be("TRACK123456789");

        var deliveredEvent = _harness.Published.Select<OrderDeliveredEvent>().FirstOrDefault();
        deliveredEvent!.Context.Message.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task OrderCancellationWorkflow_ShouldPublishExpectedEvents()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createCommand = OrderTestData.SimpleCreateCommand();
        var authenticatedClient = GetAuthenticatedClient(_client);
        var cancellationReason = "Customer changed mind during test";


        // Act - Execute order creation and cancellation
        
        // 1. Create Order
        var createResponse = await authenticatedClient.PostAsJsonAsync("/api/orders", createCommand, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        var orderId = createResult!.OrderId;

        // 2. Cancel Order
        var cancelRequest = new { Reason = cancellationReason };
        var cancelResponse = await authenticatedClient.PostAsJsonAsync($"/api/orders/{orderId}/cancel", cancelRequest, _jsonOptions);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Verify expected events were published and consumed
        (await _harness.Published.Any<OrderCreatedEvent>()).Should().BeTrue();
        (await _harness.Published.Any<OrderCancelledEvent>()).Should().BeTrue();

        (await _harness.Consumed.Any<OrderCreatedEvent>()).Should().BeTrue();
        (await _harness.Consumed.Any<OrderCancelledEvent>()).Should().BeTrue();

        // Verify cancellation event data
        var cancelledEvent = _harness.Published.Select<OrderCancelledEvent>().FirstOrDefault();
        cancelledEvent!.Context.Message.OrderId.Should().Be(orderId);
        cancelledEvent.Context.Message.CustomerId.Should().Be(createCommand.CustomerId);
        cancelledEvent.Context.Message.Reason.Should().Be(cancellationReason);
    }

    [Fact]
    public async Task MultipleOrdersWorkflow_ShouldHandleEventsIndependently()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var authenticatedClient = GetAuthenticatedClient(_client);


        // Act - Create and process multiple orders
        var orderIds = new List<Guid>();
        
        for (int i = 0; i < 3; i++)
        {
            var createCommand = OrderTestDataBuilder.Create()
                .WithCustomerId($"CUST{i:D3}")
                .WithRandomItems()
                .BuildCreateCommand();

            var createResponse = await authenticatedClient.PostAsJsonAsync("/api/orders", createCommand, _jsonOptions);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var createResult = await createResponse.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
            orderIds.Add(createResult!.OrderId);

            // Process payment for each order
            var payResponse = await authenticatedClient.PostAsync($"/api/orders/{createResult.OrderId}/pay", null);
            payResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Assert - Verify all events were published
        var createdEvents = _harness.Published.Select<OrderCreatedEvent>().ToList();
        var paidEvents = _harness.Published.Select<OrderPaidEvent>().ToList();

        createdEvents.Should().HaveCount(3);
        paidEvents.Should().HaveCount(3);

        // Verify each order ID appears in both event types
        foreach (var orderId in orderIds)
        {
            createdEvents.Should().Contain(e => e.Context.Message.OrderId == orderId);
            paidEvents.Should().Contain(e => e.Context.Message.OrderId == orderId);
        }
    }

    #endregion

    #region Helper Methods


    private HttpClient GetAuthenticatedClient(HttpClient client)
    {
        return AuthenticationTestHelper.CreateAuthenticatedClient(client, _factory.Services);
    }

    private async Task<CreateOrderResult> CreateTestOrderAsync(HttpClient client, string? customerId = null)
    {
        var command = customerId != null 
            ? OrderTestDataBuilder.Create().WithCustomerId(customerId).WithRandomItems().BuildCreateCommand()
            : OrderTestData.SimpleCreateCommand();
        
        var authenticatedClient = GetAuthenticatedClient(client);
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions) 
               ?? throw new InvalidOperationException("Failed to create test order");
    }

    private async Task<CreateOrderResult> CreateValidatedTestOrderAsync(string? customerId = null)
    {
        // First create a regular order
        var order = await CreateTestOrderAsync(_client, customerId);
        
        // Then validate it by directly manipulating the database state
        // Since there's no validation endpoint, we need to access the domain entity directly
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Include the Items collection when loading the order
        var domainOrder = await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order.OrderId);
        
        if (domainOrder == null)
            throw new InvalidOperationException("Order not found after creation");
        
        // Validate the order using domain method
        domainOrder.ValidateOrder();
        await dbContext.SaveChangesAsync();
        
        return order;
    }

    private async Task<CreateOrderResult> CreatePaidTestOrderAsync(string? customerId = null)
    {
        // First create and validate an order
        var order = await CreateValidatedTestOrderAsync(customerId);
        
        // Then process payment using the API endpoint
        var authenticatedClient = GetAuthenticatedClient(_client);
        var payResponse = await authenticatedClient.PostAsync($"/api/orders/{order.OrderId}/pay", null);
        payResponse.EnsureSuccessStatusCode();
        
        return order;
    }

    private async Task<CreateOrderResult> CreateShippedTestOrderAsync(string? customerId = null, string trackingNumber = "TEST123456789")
    {
        // First create and pay for an order
        var order = await CreatePaidTestOrderAsync(customerId);
        
        // Then ship it using the API endpoint
        var authenticatedClient = GetAuthenticatedClient(_client);
        var shipRequest = new { TrackingNumber = trackingNumber };
        var shipResponse = await authenticatedClient.PostAsJsonAsync($"/api/orders/{order.OrderId}/ship", shipRequest, _jsonOptions);
        shipResponse.EnsureSuccessStatusCode();
        
        return order;
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _factory.ClearDatabaseAsync();
        _client.Dispose();
    }
}