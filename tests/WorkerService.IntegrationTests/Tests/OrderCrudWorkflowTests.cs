using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Entities;
using WorkerService.Infrastructure.Data;
using WorkerService.IntegrationTests.Fixtures;
using WorkerService.IntegrationTests.Utilities;
using Xunit;

namespace WorkerService.IntegrationTests.Tests;

[Collection("Api Integration Tests")]
public class OrderCrudWorkflowTests : IClassFixture<ApiTestWebApplicationFactory>, IAsyncDisposable
{
    private readonly ApiTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrderCrudWorkflowTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region Complete Business Workflows

    [Fact]
    public async Task CustomerOrderJourney_CompleteLifecycle_ShouldWorkEndToEnd()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var customerId = "VIP_CUSTOMER_001";
        
        // Step 1: Customer creates an order
        var createCommand = OrderTestDataBuilder.Create()
            .WithCustomerId(customerId)
            .WithItem("LAPTOP001", 1, 999.99m)
            .WithItem("MOUSE001", 1, 29.99m)
            .BuildCreateCommand();

        // Act & Assert - Create Order
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createCommand, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        createResult.Should().NotBeNull();
        createResult!.TotalAmount.Should().Be(1029.98m);
        var orderId = createResult.OrderId;

        // Step 2: Customer reviews their order
        var getResponse = await _client.GetAsync($"/api/orders/{orderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderDetails = await getResponse.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        orderDetails.Should().NotBeNull();
        orderDetails!.Status.Should().Be("Pending");
        orderDetails.Items.Should().HaveCount(2);

        // Step 3: Customer decides to modify their order (add warranty)
        var updateCommand = OrderTestDataBuilder.Create()
            .WithCustomerId(customerId)
            .WithItem("LAPTOP001", 1, 999.99m)
            .WithItem("MOUSE001", 1, 29.99m)
            .WithItem("WARRANTY001", 1, 99.99m) // Added warranty
            .BuildUpdateCommand(orderId);

        var updateResponse = await _client.PutAsJsonAsync($"/api/orders/{orderId}", updateCommand, _jsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<UpdateOrderResult>(_jsonOptions);
        updateResult!.TotalAmount.Should().Be(1129.97m); // Updated total

        // Step 4: Customer checks their updated order
        var updatedGetResponse = await _client.GetAsync($"/api/orders/{orderId}");
        var updatedOrderDetails = await updatedGetResponse.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        updatedOrderDetails!.Items.Should().HaveCount(3);
        updatedOrderDetails.TotalAmount.Should().Be(1129.97m);

        // Step 5: Customer cancels the order
        var deleteResponse = await _client.DeleteAsync($"/api/orders/{orderId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 6: Verify order is cancelled (returns 404)
        var finalGetResponse = await _client.GetAsync($"/api/orders/{orderId}");
        finalGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkOrderProcessing_MultipleCustomers_ShouldHandleEfficiently()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var customerCount = 20;
        var ordersPerCustomer = 3;
        var totalExpectedOrders = customerCount * ordersPerCustomer;

        // Act - Create orders for multiple customers
        var createTasks = new List<Task<CreateOrderResult>>();
        
        for (int customerId = 1; customerId <= customerCount; customerId++)
        {
            for (int orderNum = 1; orderNum <= ordersPerCustomer; orderNum++)
            {
                var command = OrderTestDataBuilder.Create()
                    .WithCustomerId($"BULK_CUST_{customerId:D3}")
                    .WithRandomItems(Random.Shared.Next(1, 5))
                    .BuildCreateCommand();
                
                createTasks.Add(CreateOrderAsync(command));
            }
        }

        var createdOrders = await Task.WhenAll(createTasks);

        // Assert - Verify all orders were created
        createdOrders.Should().HaveCount(totalExpectedOrders);
        createdOrders.Should().OnlyContain(o => o.OrderId != Guid.Empty);

        // Act - Query orders with pagination
        var page1Response = await _client.GetAsync("/api/orders?pageNumber=1&pageSize=10");
        var page1Result = await page1Response.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);
        
        var page2Response = await _client.GetAsync("/api/orders?pageNumber=2&pageSize=10");
        var page2Result = await page2Response.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);

        // Assert - Verify pagination works correctly
        page1Result!.TotalCount.Should().Be(totalExpectedOrders);
        page1Result.Orders.Should().HaveCount(10);
        page1Result.HasNextPage.Should().BeTrue();
        page1Result.HasPreviousPage.Should().BeFalse();

        page2Result!.Orders.Should().HaveCount(10);
        page2Result.HasNextPage.Should().BeTrue();
        page2Result.HasPreviousPage.Should().BeTrue();

        // Act - Filter orders by specific customer
        var specificCustomer = "BULK_CUST_005";
        var filteredResponse = await _client.GetAsync($"/api/orders?customerId={specificCustomer}");
        var filteredResult = await filteredResponse.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);

        // Assert - Verify filtering works
        filteredResult!.Orders.Should().HaveCount(ordersPerCustomer);
        filteredResult.Orders.Should().OnlyContain(o => o.CustomerId == specificCustomer);
    }

    [Fact]
    public async Task OrderModificationWorkflow_ValidateBusinessRules_ShouldEnforceConstraints()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var customerId = "BUSINESS_CUSTOMER";

        // Step 1: Create an order
        var createCommand = OrderTestDataBuilder.Create()
            .WithCustomerId(customerId)
            .WithItem("PROD001", 1, 50.00m)
            .BuildCreateCommand();

        var createResult = await CreateOrderAsync(createCommand);
        var orderId = createResult.OrderId;

        // Step 2: Attempt to update with invalid data
        var invalidUpdateCommand = new UpdateOrderCommand(
            orderId,
            "", // Empty customer ID
            new List<OrderItemDto> { new("PROD001", 1, 50.00m) });

        var invalidUpdateResponse = await _client.PutAsJsonAsync($"/api/orders/{orderId}", invalidUpdateCommand, _jsonOptions);
        invalidUpdateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Step 3: Attempt to update with mismatched ID
        var mismatchedIdCommand = OrderTestDataBuilder.Create()
            .WithCustomerId(customerId)
            .WithItem("PROD001", 1, 50.00m)
            .BuildUpdateCommand(Guid.NewGuid()); // Different ID

        var mismatchedIdResponse = await _client.PutAsJsonAsync($"/api/orders/{orderId}", mismatchedIdCommand, _jsonOptions);
        mismatchedIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Step 4: Valid update should succeed
        var validUpdateCommand = OrderTestDataBuilder.Create()
            .WithCustomerId("UPDATED_CUSTOMER")
            .WithItem("PROD002", 2, 75.00m)
            .BuildUpdateCommand(orderId);

        var validUpdateResponse = await _client.PutAsJsonAsync($"/api/orders/{orderId}", validUpdateCommand, _jsonOptions);
        validUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Verify the update was applied
        var getResponse = await _client.GetAsync($"/api/orders/{orderId}");
        var orderDetails = await getResponse.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        orderDetails!.CustomerId.Should().Be("UPDATED_CUSTOMER");
        orderDetails.TotalAmount.Should().Be(150.00m);
    }

    [Fact]
    public async Task ErrorHandlingWorkflow_DatabaseConsistency_ShouldMaintainIntegrity()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        // Test 1: Create order with invalid data
        var invalidCreateCommand = OrderTestData.Invalid.EmptyCustomerId();
        var invalidCreateResponse = await _client.PostAsJsonAsync("/api/orders", invalidCreateCommand, _jsonOptions);
        invalidCreateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test 2: Get non-existent order
        var nonExistentId = Guid.NewGuid();
        var nonExistentGetResponse = await _client.GetAsync($"/api/orders/{nonExistentId}");
        nonExistentGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test 3: Update non-existent order
        var nonExistentUpdateCommand = OrderTestData.SimpleUpdateCommand(nonExistentId);
        var nonExistentUpdateResponse = await _client.PutAsJsonAsync($"/api/orders/{nonExistentId}", nonExistentUpdateCommand, _jsonOptions);
        nonExistentUpdateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Test 4: Delete non-existent order
        var nonExistentDeleteResponse = await _client.DeleteAsync($"/api/orders/{nonExistentId}");
        nonExistentDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify database is still in consistent state
        var allOrdersResponse = await _client.GetAsync("/api/orders");
        var allOrdersResult = await allOrdersResponse.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);
        allOrdersResult!.TotalCount.Should().Be(0); // No orders should have been created
    }

    [Fact]
    public async Task LargeOrderWorkflow_PerformanceAndScaling_ShouldHandleEfficiently()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var customerId = "LARGE_ORDER_CUSTOMER";

        // Create an order with many items
        var builder = OrderTestDataBuilder.Create().WithCustomerId(customerId);
        for (int i = 1; i <= 100; i++)
        {
            builder.WithItem($"PROD{i:D3}", Random.Shared.Next(1, 10), Random.Shared.Next(1, 100));
        }
        var largeOrderCommand = builder.BuildCreateCommand();

        var startTime = DateTime.UtcNow;

        // Act - Create large order
        var createResponse = await _client.PostAsJsonAsync("/api/orders", largeOrderCommand, _jsonOptions);
        
        var createDuration = DateTime.UtcNow - startTime;

        // Assert - Order created successfully and within reasonable time
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createDuration.Should().BeLessThan(TimeSpan.FromSeconds(10)); // Performance assertion

        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        createResult!.TotalAmount.Should().BeGreaterThan(0);

        // Verify order details
        var getResponse = await _client.GetAsync($"/api/orders/{createResult.OrderId}");
        var orderDetails = await getResponse.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        orderDetails!.Items.Should().HaveCount(100);

        // Update the large order
        var updateStartTime = DateTime.UtcNow;
        var updateCommand = OrderTestDataBuilder.Create()
            .WithCustomerId(customerId)
            .WithItem("UPDATED_PROD001", 1, 999.99m)
            .BuildUpdateCommand(createResult.OrderId);

        var updateResponse = await _client.PutAsJsonAsync($"/api/orders/{createResult.OrderId}", updateCommand, _jsonOptions);
        var updateDuration = DateTime.UtcNow - updateStartTime;

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updateDuration.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Update should be faster
    }

    [Fact]
    public async Task DataConsistencyWorkflow_ConcurrentAccess_ShouldMaintainIntegrity()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var customerId = "CONCURRENT_ACCESS_CUSTOMER";
        
        // Create an initial order
        var createCommand = OrderTestDataBuilder.Create()
            .WithCustomerId(customerId)
            .WithItem("PROD001", 1, 100.00m)
            .BuildCreateCommand();

        var createResult = await CreateOrderAsync(createCommand);
        var orderId = createResult.OrderId;

        // Act - Attempt concurrent updates
        var updateTasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 10; i++)
        {
            var updateCommand = OrderTestDataBuilder.Create()
                .WithCustomerId($"{customerId}_UPDATE_{i}")
                .WithItem($"PROD{i:D3}", 1, 50.00m + i)
                .BuildUpdateCommand(orderId);

            updateTasks.Add(_client.PutAsJsonAsync($"/api/orders/{orderId}", updateCommand, _jsonOptions));
        }

        var updateResponses = await Task.WhenAll(updateTasks);

        // Assert - At least one update should succeed
        var successfulUpdates = updateResponses.Count(r => r.StatusCode == HttpStatusCode.OK);
        successfulUpdates.Should().BeGreaterThan(0);

        // Verify final state is consistent
        var finalGetResponse = await _client.GetAsync($"/api/orders/{orderId}");
        finalGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var finalOrderDetails = await finalGetResponse.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        finalOrderDetails.Should().NotBeNull();
        finalOrderDetails!.TotalAmount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Performance and Load Tests

    [Fact]
    public async Task HighVolumeOperations_StressTest_ShouldMaintainPerformance()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var operationCount = 50;
        var maxDurationPerOperation = TimeSpan.FromSeconds(2);

        // Act & Assert - High volume creates
        var createTasks = new List<Task<(TimeSpan Duration, bool Success)>>();
        
        for (int i = 0; i < operationCount; i++)
        {
            createTasks.Add(MeasureOperationAsync(async () =>
            {
                var command = OrderTestDataBuilder.Create()
                    .WithCustomerId($"STRESS_CUST_{i:D3}")
                    .WithRandomItems()
                    .BuildCreateCommand();
                
                var response = await _client.PostAsJsonAsync("/api/orders", command, _jsonOptions);
                return response.IsSuccessStatusCode;
            }));
        }

        var createResults = await Task.WhenAll(createTasks);

        // Verify performance and success rate
        createResults.Should().AllSatisfy(r => 
        {
            r.Duration.Should().BeLessThan(maxDurationPerOperation);
            r.Success.Should().BeTrue();
        });

        // Test list performance with large dataset
        var listStartTime = DateTime.UtcNow;
        var listResponse = await _client.GetAsync("/api/orders?pageSize=100");
        var listDuration = DateTime.UtcNow - listStartTime;

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listDuration.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    #endregion

    #region Helper Methods

    private async Task<CreateOrderResult> CreateOrderAsync(CreateOrderCommand command)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", command, _jsonOptions);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions) 
               ?? throw new InvalidOperationException("Failed to create order");
    }

    private async Task<(TimeSpan Duration, T Result)> MeasureOperationAsync<T>(Func<Task<T>> operation)
    {
        var startTime = DateTime.UtcNow;
        var result = await operation();
        var duration = DateTime.UtcNow - startTime;
        
        return (duration, result);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _client.Dispose();
    }
}