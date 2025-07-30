using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Infrastructure.Data;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using WorkerService.IntegrationTests.Shared.Fixtures;
using WorkerService.IntegrationTests.Shared.Utilities;
using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Tests;

[Collection("InMemory Integration Tests")]
public class OrdersApiInMemoryTests : IClassFixture<InMemoryWebApplicationFactory>, IAsyncDisposable
{
    private readonly InMemoryWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrdersApiInMemoryTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region Authentication Tests

    [Fact]
    public async Task OrdersEndpoints_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var command = OrderTestData.SimpleCreateCommand();

        // Act & Assert - All endpoints should return 401 without authentication
        var createResponse = await _client.PostAsJsonAsync("/api/orders", command, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getResponse = await _client.GetAsync("/api/orders");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getByIdResponse = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updateResponse = await _client.PutAsJsonAsync($"/api/orders/{Guid.NewGuid()}", command, _jsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var deleteResponse = await _client.DeleteAsync($"/api/orders/{Guid.NewGuid()}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OrdersEndpoints_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var command = OrderTestData.SimpleCreateCommand();
        
        // Add invalid token
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act & Assert
        var createResponse = await _client.PostAsJsonAsync("/api/orders", command, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getResponse = await _client.GetAsync("/api/orders");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OrdersEndpoints_WithValidToken_ShouldReturnSuccess()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var authenticatedClient = AuthenticationTestHelper.CreateAuthenticatedClient(_client, _factory.Services);

        // Act & Assert - Test basic endpoint access with valid token
        var getResponse = await authenticatedClient.GetAsync("/api/orders");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region POST /api/orders Tests

    [Fact]
    public async Task CreateOrder_WithValidData_ShouldReturnCreatedWithLocation()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var command = OrderTestData.SimpleCreateCommand();
        var authenticatedClient = GetAuthenticatedClient();

        // First test health endpoint to verify server is running
        var healthResponse = await authenticatedClient.GetAsync("/health");
        
        if (healthResponse.StatusCode != HttpStatusCode.OK)
        {
            var healthContent = await healthResponse.Content.ReadAsStringAsync();
            throw new Exception($"Health check failed with {healthResponse.StatusCode}: {healthContent}");
        }
        
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - test actual endpoint
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        
        var result = await response.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(command.CustomerId);
        result.TotalAmount.Should().Be(command.Items.Sum(i => i.Quantity * i.UnitPrice));
        result.OrderId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var command = OrderTestData.Invalid.EmptyCustomerId();
        var authenticatedClient = GetAuthenticatedClient();

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);
        problemDetails.Should().NotBeNull();
        problemDetails!.Errors.Should().ContainKey("CustomerId");
    }

    [Fact]
    public async Task CreateOrder_WithEmptyItems_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var command = OrderTestData.Invalid.EmptyItems();
        var authenticatedClient = GetAuthenticatedClient();

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithComplexOrder_ShouldCalculateTotalCorrectly()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var command = OrderTestData.ComplexCreateCommand();
        var expectedTotal = command.Items.Sum(i => i.Quantity * i.UnitPrice);

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        result!.TotalAmount.Should().Be(expectedTotal);
    }

    #endregion

    #region GET /api/orders/{id} Tests

    [Fact]
    public async Task GetOrder_WithExistingId_ShouldReturnOrder()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreateTestOrderAsync();
        var authenticatedClient = GetAuthenticatedClient();

        // Act
        var response = await authenticatedClient.GetAsync($"/api/orders/{order.OrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.OrderId);
        result.CustomerId.Should().Be(order.CustomerId);
        result.TotalAmount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task GetOrder_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.GetAsync($"/api/orders/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/orders Tests

    [Fact]
    public async Task GetOrders_WithDefaultParameters_ShouldReturnPagedResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateMultipleTestOrdersAsync(5);
        var authenticatedClient = GetAuthenticatedClient();

        // Act
        var response = await authenticatedClient.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Orders.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrders_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateMultipleTestOrdersAsync(25);

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.GetAsync("/api/orders?pageNumber=2&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Orders.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrders_WithCustomerFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var targetCustomerId = "FILTERED_CUSTOMER";
        
        // Create orders for different customers
        await CreateTestOrderAsync("CUST001");
        await CreateTestOrderAsync("CUST002");
        await CreateTestOrderAsync(targetCustomerId);
        await CreateTestOrderAsync(targetCustomerId);

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.GetAsync($"/api/orders?customerId={targetCustomerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Orders.Should().HaveCount(2);
        result.Orders.Should().OnlyContain(o => o.CustomerId == targetCustomerId);
    }

    [Theory]
    [InlineData(0, 20)] // Invalid page number
    [InlineData(-1, 20)] // Negative page number
    public async Task GetOrders_WithInvalidPageNumber_ShouldReturnBadRequest(int pageNumber, int pageSize)
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var authenticatedClient = GetAuthenticatedClient();

        // Act
        var response = await authenticatedClient.GetAsync($"/api/orders?pageNumber={pageNumber}&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(1, 0)] // Invalid page size
    [InlineData(1, -1)] // Negative page size
    [InlineData(1, 101)] // Page size too large
    public async Task GetOrders_WithInvalidPageSize_ShouldReturnBadRequest(int pageNumber, int pageSize)
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var authenticatedClient = GetAuthenticatedClient();

        // Act
        var response = await authenticatedClient.GetAsync($"/api/orders?pageNumber={pageNumber}&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT /api/orders/{id} Tests

    [Fact]
    public async Task UpdateOrder_WithValidData_ShouldReturnUpdatedOrder()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var originalOrder = await CreateTestOrderAsync();
        var updateCommand = OrderTestData.SimpleUpdateCommand(originalOrder.OrderId);

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.PutAsJsonAsync($"/api/orders/{originalOrder.OrderId}", updateCommand, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<UpdateOrderResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(originalOrder.OrderId);
        result.CustomerId.Should().Be(updateCommand.CustomerId);
        result.TotalAmount.Should().Be(updateCommand.Items.Sum(i => i.Quantity * i.UnitPrice));
    }

    [Fact]
    public async Task UpdateOrder_WithMismatchedIds_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var urlId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var updateCommand = OrderTestData.SimpleUpdateCommand(commandId);

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.PutAsJsonAsync($"/api/orders/{urlId}", updateCommand, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateOrder_WithNonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var updateCommand = OrderTestData.SimpleUpdateCommand(nonExistentId);

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.PutAsJsonAsync($"/api/orders/{nonExistentId}", updateCommand, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrder_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var originalOrder = await CreateTestOrderAsync();
        var invalidCommand = new UpdateOrderCommand(originalOrder.OrderId, "", new List<OrderItemDto>());

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.PutAsJsonAsync($"/api/orders/{originalOrder.OrderId}", invalidCommand, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);
        problemDetails.Should().NotBeNull();
    }

    #endregion

    #region DELETE /api/orders/{id} Tests

    [Fact]
    public async Task DeleteOrder_WithExistingOrder_ShouldReturnNoContent()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var order = await CreateTestOrderAsync();

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.DeleteAsync($"/api/orders/{order.OrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Verify order is deleted by trying to get it
        var getResponse = await authenticatedClient.GetAsync($"/api/orders/{order.OrderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteOrder_WithNonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.DeleteAsync($"/api/orders/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    #endregion

    #region End-to-End Workflow Tests

    [Fact]
    public async Task CompleteOrderWorkflow_CreateGetUpdateDelete_ShouldWorkCorrectly()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createCommand = OrderTestData.SimpleCreateCommand();

        // Act & Assert - Create
        var authenticatedClient = GetAuthenticatedClient();
        var createResponse = await authenticatedClient.PostAsJsonAsync("/api/orders", createCommand, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions);
        var orderId = createResult!.OrderId;

        // Act & Assert - Get
        var getResponse = await authenticatedClient.GetAsync($"/api/orders/{orderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<OrderResponseDto>(_jsonOptions);
        getResult!.Id.Should().Be(orderId);

        // Act & Assert - Update
        var updateCommand = OrderTestData.SimpleUpdateCommand(orderId);
        var updateResponse = await authenticatedClient.PutAsJsonAsync($"/api/orders/{orderId}", updateCommand, _jsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<UpdateOrderResult>(_jsonOptions);
        updateResult!.CustomerId.Should().Be(updateCommand.CustomerId);

        // Act & Assert - Delete
        var deleteResponse = await authenticatedClient.DeleteAsync($"/api/orders/{orderId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var finalGetResponse = await authenticatedClient.GetAsync($"/api/orders/{orderId}");
        finalGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldHandleCorrectly()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Create multiple orders concurrently
        var authenticatedClient = GetAuthenticatedClient();
        for (int i = 0; i < 10; i++)
        {
            var command = OrderTestDataBuilder.Create()
                .WithCustomerId($"CONCURRENT_CUST_{i:D3}")
                .WithRandomItems()
                .BuildCreateCommand();
            
            tasks.Add(authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
        
        // Verify all orders were created
        var listResponse = await authenticatedClient.GetAsync("/api/orders?pageSize=20");
        var listResult = await listResponse.Content.ReadFromJsonAsync<PagedOrdersResult>(_jsonOptions);
        listResult!.TotalCount.Should().Be(10);
    }

    #endregion

    #region Helper Methods

    private HttpClient GetAuthenticatedClient()
    {
        return AuthenticationTestHelper.CreateAuthenticatedClient(_client, _factory.Services);
    }

    private async Task<CreateOrderResult> CreateTestOrderAsync(string? customerId = null)
    {
        var command = customerId != null 
            ? OrderTestDataBuilder.Create().WithCustomerId(customerId).WithRandomItems().BuildCreateCommand()
            : OrderTestData.SimpleCreateCommand();
        
        var authenticatedClient = GetAuthenticatedClient();
        var response = await authenticatedClient.PostAsJsonAsync("/api/orders", command, _jsonOptions);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CreateOrderResult>(_jsonOptions) 
               ?? throw new InvalidOperationException("Failed to create test order");
    }

    private async Task CreateMultipleTestOrdersAsync(int count)
    {
        var tasks = new List<Task>();
        for (int i = 0; i < count; i++)
        {
            tasks.Add(CreateTestOrderAsync($"CUST{i:D3}"));
        }
        await Task.WhenAll(tasks);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _factory.ClearDatabaseAsync();
        _client.Dispose();
    }
}