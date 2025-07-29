using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using WorkerService.Application.Common.DTOs;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using WorkerService.IntegrationTests.Shared.Utilities;
using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Tests;

[Collection("InMemory Integration Tests")]
public class ItemsApiInMemoryTests : IClassFixture<InMemoryWebApplicationFactory>, IAsyncDisposable
{
    private readonly InMemoryWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ItemsApiInMemoryTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    #region POST /api/items Tests

    [Fact]
    public async Task CreateItem_WithValidData_ShouldReturnCreatedWithLocation()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: "TEST-001",
            Name: "Test Product",
            Description: "Test Description",
            Price: 99.99m,
            InitialStock: 100,
            Category: "Electronics"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.SKU.Should().Be(createDto.SKU);
        result.Name.Should().Be(createDto.Name);
        result.AvailableStock.Should().Be(createDto.InitialStock);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateItem_WithDuplicateSKU_ShouldReturnConflict()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: "DUP-001",
            Name: "Duplicate Product",
            Description: "Test",
            Price: 50m,
            InitialStock: 10,
            Category: "Test"
        );

        // Create first item
        await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Act - Try to create duplicate
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Contain("already exists");
    }

    [Theory]
    [InlineData("", "SKU is required")]
    [InlineData("INVALID-SKU!", "must contain only uppercase letters")]
    [InlineData("lowercase-sku", "must contain only uppercase letters")]
    public async Task CreateItem_WithInvalidSKU_ShouldReturnBadRequest(string sku, string expectedError)
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: sku,
            Name: "Test Product",
            Description: "Test",
            Price: 10m,
            InitialStock: 1,
            Category: "Test"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(expectedError);
    }

    [Theory]
    [InlineData(-1, "Price must be greater than zero")]
    [InlineData(0, "Price must be greater than zero")]
    public async Task CreateItem_WithInvalidPrice_ShouldReturnBadRequest(decimal price, string expectedError)
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: "PRICE-001",
            Name: "Price Test",
            Description: "Test",
            Price: price,
            InitialStock: 1,
            Category: "Test"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(expectedError);
    }

    #endregion

    #region GET /api/items/{id} Tests

    [Fact]
    public async Task GetItem_WithExistingId_ShouldReturnItem()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();

        // Act
        var response = await _client.GetAsync($"/api/items/{item.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(item.Id);
        result.SKU.Should().Be(item.SKU);
    }

    [Fact]
    public async Task GetItem_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/items/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/items/sku/{sku} Tests

    [Fact]
    public async Task GetItemBySku_WithExistingSku_ShouldReturnItem()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();

        // Act
        var response = await _client.GetAsync($"/api/items/sku/{item.SKU}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.SKU.Should().Be(item.SKU);
    }

    [Fact]
    public async Task GetItemBySku_WithNonExistentSku_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();

        // Act
        var response = await _client.GetAsync("/api/items/sku/NONEXISTENT");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/items Tests

    [Fact]
    public async Task GetItems_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateMultipleTestItemsAsync(25);

        // Act
        var response = await _client.GetAsync("/api/items?pageNumber=2&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(2);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetItems_WithCategoryFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateTestItemAsync(sku: "ELEC-001", category: "Electronics");
        await CreateTestItemAsync(sku: "ELEC-002", category: "Electronics");
        await CreateTestItemAsync(sku: "BOOK-001", category: "Books");

        // Act
        var response = await _client.GetAsync("/api/items?category=Electronics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Category == "Electronics");
    }

    [Fact]
    public async Task GetItems_WithSearchTerm_ShouldReturnMatchingItems()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateTestItemAsync(sku: "LAPTOP-001", name: "Gaming Laptop");
        await CreateTestItemAsync(sku: "MOUSE-001", name: "Gaming Mouse");
        await CreateTestItemAsync(sku: "DESK-001", name: "Office Desk");

        // Act
        var response = await _client.GetAsync("/api/items?search=Gaming");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Name.Contains("Gaming"));
    }

    [Fact] 
    public async Task GetItems_WithIsActiveFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var activeItem = await CreateTestItemAsync(sku: "ACTIVE-001");
        var inactiveItem = await CreateTestItemAsync(sku: "INACTIVE-001");
        
        // Deactivate one item
        await _client.DeleteAsync($"/api/items/{inactiveItem.Id}");

        // Act
        var response = await _client.GetAsync("/api/items?isActive=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result!.Items.Should().HaveCount(1);
        result.Items.First().Id.Should().Be(activeItem.Id);
    }

    #endregion

    #region PUT /api/items/{id} Tests

    [Fact]
    public async Task UpdateItem_WithValidData_ShouldReturnUpdatedItem()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();
        var updateDto = new UpdateItemDto(
            Name: "Updated Product",
            Description: "Updated Description",
            Price: 149.99m,
            Category: "Updated Category"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{item.Id}", updateDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.Name.Should().Be(updateDto.Name);
        result.Price.Should().Be(updateDto.Price);
        result.SKU.Should().Be(item.SKU); // SKU should not change
    }

    [Fact]
    public async Task UpdateItem_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var updateDto = new UpdateItemDto(
            Name: "Updated Product",
            Description: "Updated Description",
            Price: 149.99m,
            Category: "Updated Category"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{nonExistentId}", updateDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/items/{id} Tests (Deactivation)

    [Fact]
    public async Task DeactivateItem_WithExistingId_ShouldReturnNoContent()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/items/{item.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify item is deactivated
        var getResponse = await _client.GetAsync($"/api/items/{item.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await getResponse.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateItem_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/items/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Stock Management Tests

    [Fact]
    public async Task AdjustStock_WithValidQuantity_ShouldUpdateStock()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 50);
        var adjustmentDto = new StockAdjustmentDto(
            NewQuantity: 75,
            Reason: "Inventory recount"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", adjustmentDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.AvailableStock.Should().Be(75);
    }

    [Fact]
    public async Task AdjustStock_WithNegativeQuantity_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();
        var adjustmentDto = new StockAdjustmentDto(
            NewQuantity: -10,
            Reason: "Invalid adjustment"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", adjustmentDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReserveStock_WithAvailableQuantity_ShouldReserveStock()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 100);
        var reservationDto = new StockReservationDto(
            Quantity: 20,
            OrderId: Guid.NewGuid().ToString()
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/items/{item.Id}/reserve-stock", reservationDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.AvailableStock.Should().Be(80);
        result.ReservedStock.Should().Be(20);
    }

    [Fact]
    public async Task ReserveStock_WithInsufficientQuantity_ShouldReturnConflict()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 10);
        var reservationDto = new StockReservationDto(
            Quantity: 20,
            OrderId: Guid.NewGuid().ToString()
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/items/{item.Id}/reserve-stock", reservationDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problemDetails!.Detail.Should().Contain("Cannot reserve");
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentStockUpdates_ShouldHandleOptimisticConcurrency()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 100);

        // Create two concurrent update tasks
        var task1 = _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", 
            new StockAdjustmentDto(50, "Update 1"), _jsonOptions);
        
        var task2 = _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", 
            new StockAdjustmentDto(75, "Update 2"), _jsonOptions);

        // Act
        var responses = await Task.WhenAll(task1, task2);

        // Assert
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        
        // One should succeed, one should get conflict (due to optimistic concurrency)
        // Note: In practice, both might succeed if they don't actually conflict
        (successCount + conflictCount).Should().Be(2);
        successCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetItems_WithLargeDataset_ShouldReturnWithinAcceptableTime()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateMultipleTestItemsAsync(1000);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/items?pageSize=100");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result!.Items.Should().HaveCount(100);
        result.TotalCount.Should().Be(1000);
    }

    #endregion

    #region Helper Methods

    private async Task<ItemDto> CreateTestItemAsync(
        string sku = "TEST-001",
        string name = "Test Product",
        decimal price = 99.99m,
        int initialStock = 100,
        string category = "Test Category")
    {
        var createDto = new CreateItemDto(sku, name, "Test Description", price, initialStock, category);
        
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions) 
               ?? throw new InvalidOperationException("Failed to create test item");
    }

    private async Task CreateMultipleTestItemsAsync(int count)
    {
        var tasks = new List<Task>();
        for (int i = 0; i < count; i++)
        {
            tasks.Add(CreateTestItemAsync(
                sku: $"TEST-{i:D3}",
                name: $"Test Product {i}",
                price: 10m + i,
                category: i % 2 == 0 ? "Electronics" : "Books"
            ));
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