using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Application.Commands;
using WorkerService.Application.Handlers;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Handlers;

public class UpdateItemCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockItemRepository;
    private readonly Mock<ILogger<UpdateItemCommandHandler>> _mockLogger;
    private readonly UpdateItemCommandHandler _handler;

    public UpdateItemCommandHandlerTests()
    {
        _mockItemRepository = new Mock<IItemRepository>();
        _mockLogger = new Mock<ILogger<UpdateItemCommandHandler>>();
        
        _handler = new UpdateItemCommandHandler(
            _mockItemRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateItemAndReturnDto()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        _mockItemRepository
            .Setup(r => r.UpdateAsync(existingItem, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(itemId);
        result.Name.Should().Be("Updated Product Name");
        result.Description.Should().Be("Updated Description");
        result.Price.Should().Be(35.99m);
        result.Category.Should().Be("Updated Category");

        // Verify repository interactions
        _mockItemRepository.Verify(r => r.GetByIdAsync(itemId, cancellationToken), Times.Once);
        _mockItemRepository.Verify(r => r.UpdateAsync(existingItem, cancellationToken), Times.Once);
        _mockItemRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenItemNotFound_ShouldReturnNull()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync((Item?)null);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().BeNull();

        // Verify no update operations were called
        _mockItemRepository.Verify(r => r.UpdateAsync(It.IsAny<Item>(), cancellationToken), Times.Never);
        _mockItemRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task Handle_WhenUpdateThrows_ShouldPropagateException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Update failed");

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        _mockItemRepository
            .Setup(r => r.UpdateAsync(existingItem, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Update failed");
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrows_ShouldPropagateException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Save failed");

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        _mockItemRepository
            .Setup(r => r.UpdateAsync(existingItem, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Save failed");
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        _mockItemRepository
            .Setup(r => r.UpdateAsync(existingItem, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Updating item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("updated successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenItemNotFound_ShouldLogWarning()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync((Item?)null);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNegativePrice_ShouldThrowArgumentException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            -10.99m, // Negative price
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Price cannot be negative*");
    }

    [Fact]
    public async Task Handle_WithInactiveItem_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        existingItem.Deactivate(); // Make item inactive
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot update inactive item");
    }

    [Fact]
    public async Task Handle_ShouldUpdateOnlyChangedProperties()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        var originalName = existingItem.Name;
        var originalDescription = existingItem.Description;
        var originalPrice = existingItem.Price;
        var originalCategory = existingItem.Category;
        
        var command = new UpdateItemCommand(
            itemId,
            originalName, // Same name
            "Updated Description", // Changed description
            originalPrice.Amount, // Same price
            originalCategory); // Same category

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        _mockItemRepository
            .Setup(r => r.UpdateAsync(existingItem, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(originalName);
        result.Description.Should().Be("Updated Description");
        result.Price.Should().Be(originalPrice.Amount);
        result.Category.Should().Be(originalCategory);
    }

    [Fact]
    public async Task Handle_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "", // Empty name
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Item name cannot be empty*");
    }

    [Fact]
    public async Task Handle_WithLongName_ShouldThrowArgumentException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        var longName = new string('A', 201); // Exceeds 200 character limit
        
        var command = new UpdateItemCommand(
            itemId,
            longName,
            "Updated Description",
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Item name cannot exceed 200 characters*");
    }

    [Fact]
    public async Task Handle_WithNullDescription_ShouldSetEmptyString()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existingItem = CreateTestItem(itemId);
        
        var command = new UpdateItemCommand(
            itemId,
            "Updated Product Name",
            null!, // Null description
            35.99m,
            "Updated Category");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.GetByIdAsync(itemId, cancellationToken))
            .ReturnsAsync(existingItem);

        _mockItemRepository
            .Setup(r => r.UpdateAsync(existingItem, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be(string.Empty);
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