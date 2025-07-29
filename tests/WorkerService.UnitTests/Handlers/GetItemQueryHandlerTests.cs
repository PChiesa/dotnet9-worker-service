using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Application.Handlers;
using WorkerService.Application.Queries;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Handlers;

public class GetItemQueryHandlerTests
{
    private readonly Mock<IItemRepository> _mockItemRepository;
    private readonly Mock<ILogger<GetItemQueryHandler>> _mockLogger;
    private readonly GetItemQueryHandler _handler;

    public GetItemQueryHandlerTests()
    {
        _mockItemRepository = new Mock<IItemRepository>();
        _mockLogger = new Mock<ILogger<GetItemQueryHandler>>();
        
        _handler = new GetItemQueryHandler(
            _mockItemRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingItem_ShouldReturnItemDto()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(itemId);
        result.SKU.Should().Be("TEST-001");
        result.Name.Should().Be("Test Product");
        result.Description.Should().Be("Test Description");
        result.Price.Should().Be(25.99m);
        result.AvailableStock.Should().Be(100);
        result.Category.Should().Be("Electronics");
        result.IsActive.Should().BeTrue();

        // Verify repository interaction
        _mockItemRepository.Verify(r => r.GetByIdAsync(itemId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentItem_ShouldReturnNull()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync((Item?)null);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().BeNull();

        // Verify repository interaction
        _mockItemRepository.Verify(r => r.GetByIdAsync(itemId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(query, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessage()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Getting item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithInactiveItem_ShouldReturnItemDto()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        existingItem.Deactivate(); // Make item inactive
        
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithItemWithReservedStock_ShouldReturnCorrectStockLevel()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        existingItem.ReserveStock(25); // Reserve some stock
        
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.AvailableStock.Should().Be(75); // Should return available stock (100 - 25)
    }

    [Fact]
    public async Task Handle_WithValidEmptyGuid_ShouldCallRepository()
    {
        // Arrange
        var itemId = Guid.Empty;
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync((Item?)null);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().BeNull();
        _mockItemRepository.Verify(r => r.GetByIdAsync(itemId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUseCorrectCancellationToken()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var query = new GetItemQuery(itemId);
        var cancellationToken = new CancellationToken();

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync((Item?)null);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockItemRepository.Verify(r => r.GetByIdAsync(itemId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUpdatedItem_ShouldReturnLatestData()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        existingItem.Update("Updated Name", "Updated Description", new Price(99.99m), "Updated Category");
        
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
        result.Price.Should().Be(99.99m);
        result.Category.Should().Be("Updated Category");
    }

    [Fact]
    public async Task Handle_WithItemWithZeroStock_ShouldReturnCorrectStockLevel()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        existingItem.AdjustStock(0); // Set stock to zero
        
        var query = new GetItemQuery(itemId);
        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.AvailableStock.Should().Be(0);
    }

    private static Item CreateTestItem(Guid? itemId = null)
    {
        var item = new Item(
            new SKU("TEST-001"),
            "Test Product",
            "Test Description",
            new Price(25.99m),
            100,
            "Electronics"
        );

        if (itemId.HasValue)
        {
            // Use reflection to set the private Id property for testing
            var idProperty = typeof(Item).GetProperty("Id");
            idProperty?.SetValue(item, itemId.Value);
        }

        return item;
    }
}