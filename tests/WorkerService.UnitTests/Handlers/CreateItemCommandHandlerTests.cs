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

public class CreateItemCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockItemRepository;
    private readonly Mock<ILogger<CreateItemCommandHandler>> _mockLogger;
    private readonly CreateItemCommandHandler _handler;

    public CreateItemCommandHandlerTests()
    {
        _mockItemRepository = new Mock<IItemRepository>();
        _mockLogger = new Mock<ILogger<CreateItemCommandHandler>>();
        
        _handler = new CreateItemCommandHandler(
            _mockItemRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateItemAndReturnDto()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Item>());

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.SKU.Should().Be("PROD-001");
        result.Name.Should().Be("Test Product");
        result.Description.Should().Be("Test Description");
        result.Price.Should().Be(25.99m);
        result.AvailableStock.Should().Be(100);
        result.Category.Should().Be("Electronics");
        result.IsActive.Should().BeTrue();
        result.Id.Should().NotBeEmpty();

        // Verify repository interactions
        _mockItemRepository.Verify(r => r.SkuExistsAsync(command.SKU, cancellationToken), Times.Once);
        _mockItemRepository.Verify(r => r.AddAsync(It.IsAny<Item>(), cancellationToken), Times.Once);
        _mockItemRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSkuAlreadyExists_ShouldThrowException()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(true);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Item with SKU 'PROD-001' already exists");

        // Verify no item was added
        _mockItemRepository.Verify(r => r.AddAsync(It.IsAny<Item>(), cancellationToken), Times.Never);
        _mockItemRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrows_ShouldPropagateException()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Save failed");

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Item>());

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Save failed");
    }

    [Fact]
    public async Task Handle_ShouldMapCommandToEntityCorrectly()
    {
        // Arrange
        var command = new CreateItemCommand(
            "ELECTRONICS-PHONE-001",
            "Smartphone XL",
            "Latest smartphone with advanced features",
            899.99m,
            50,
            "Electronics");

        var cancellationToken = CancellationToken.None;
        Item? capturedItem = null;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .Callback<Item, CancellationToken>((item, ct) => capturedItem = item)
            .ReturnsAsync(It.IsAny<Item>());

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        capturedItem.Should().NotBeNull();
        capturedItem!.SKU.Value.Should().Be("ELECTRONICS-PHONE-001");
        capturedItem.Name.Should().Be("Smartphone XL");
        capturedItem.Description.Should().Be("Latest smartphone with advanced features");
        capturedItem.Price.Amount.Should().Be(899.99m);
        capturedItem.StockLevel.Available.Should().Be(50);
        capturedItem.StockLevel.Reserved.Should().Be(0);
        capturedItem.Category.Should().Be("Electronics");
        capturedItem.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Item>());

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
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Creating item with SKU")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("created successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ShouldLogError()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database error");

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>();

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to create item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("INVALID-sku")] // lowercase
    [InlineData("INVALID_SKU")] // underscore
    [InlineData("INVALID SKU")] // space
    public async Task Handle_WithInvalidSku_ShouldThrowArgumentException(string invalidSku)
    {
        // Arrange
        var command = new CreateItemCommand(
            invalidSku,
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("SKU must contain only uppercase letters, numbers, and hyphens*");
    }

    [Fact]
    public async Task Handle_WithNegativePrice_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            -10.99m, // Negative price
            100,
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Price cannot be negative*");
    }

    [Fact]
    public async Task Handle_WithNegativeInitialStock_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            -10, // Negative initial stock
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Available stock cannot be negative*");
    }

    [Fact]
    public async Task Handle_WithZeroPrice_ShouldSucceed()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Free Sample",
            "Free sample product",
            0.00m,
            100,
            "Samples");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Item>());

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().Be(0.00m);
    }

    [Fact]
    public async Task Handle_WithZeroInitialStock_ShouldSucceed()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Out of Stock Item",
            "Currently out of stock",
            25.99m,
            0,
            "Electronics");

        var cancellationToken = CancellationToken.None;

        _mockItemRepository
            .Setup(r => r.SkuExistsAsync(command.SKU, cancellationToken))
            .ReturnsAsync(false);

        _mockItemRepository
            .Setup(r => r.AddAsync(It.IsAny<Item>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Item>());

        _mockItemRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.AvailableStock.Should().Be(0);
    }
}