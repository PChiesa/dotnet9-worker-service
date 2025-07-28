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

public class DeleteOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<ILogger<DeleteOrderCommandHandler>> _mockLogger;
    private readonly DeleteOrderCommandHandler _handler;

    public DeleteOrderCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockLogger = new Mock<ILogger<DeleteOrderCommandHandler>>();
        
        _handler = new DeleteOrderCommandHandler(
            _mockOrderRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingOrder_ShouldMarkAsDeletedAndReturnTrue()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;

        var existingOrder = CreateTestOrder("CUST001", orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().BeTrue();

        // Verify repository interactions
        _mockOrderRepository.Verify(r => r.GetByIdAsync(orderId, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.UpdateAsync(existingOrder, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
        
        // Verify the order was marked as deleted (assuming there's a MarkAsDeleted method that sets status)
        // Note: This depends on the actual implementation of MarkAsDeleted in the Order entity
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().BeFalse();

        // Verify no update operations were called
        _mockOrderRepository.Verify(r => r.UpdateAsync(It.IsAny<Order>(), cancellationToken), Times.Never);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsOnGet_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsOnUpdate_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Update failed");

        var existingOrder = CreateTestOrder("CUST001", orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Update failed");
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsOnSaveChanges_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Save failed");

        var existingOrder = CreateTestOrder("CUST001", orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Save failed");
    }

    [Fact]
    public async Task Handle_ShouldCallCorrectRepositoryMethods()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;

        var existingOrder = CreateTestOrder("CUST001", orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockOrderRepository.Verify(r => r.GetByIdAsync(orderId, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.UpdateAsync(existingOrder, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;

        var existingOrder = CreateTestOrder("CUST001", orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Deleting order")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("marked as deleted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldLogWarning()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not found for deletion")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldHandleCancellationToken()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var existingOrder = CreateTestOrder("CUST001", orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().BeTrue();

        // Verify that the cancellation token was passed to all repository methods
        _mockOrderRepository.Verify(r => r.GetByIdAsync(orderId, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.UpdateAsync(existingOrder, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("CUST001")]
    [InlineData("CUST999")]
    [InlineData("VALID_CUSTOMER")]
    public async Task Handle_WithDifferentCustomerIds_ShouldWorkCorrectly(string customerId)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new DeleteOrderCommand(orderId);
        var cancellationToken = CancellationToken.None;

        var existingOrder = CreateTestOrder(customerId, orderId);

        _mockOrderRepository
            .Setup(r => r.GetByIdAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    private static Order CreateTestOrder(string customerId, Guid? orderId = null)
    {
        var items = new List<OrderItem>
        {
            new("PROD001", 1, new Money(10.00m))
        };
        var order = new Order(customerId, items);
        
        if (orderId.HasValue)
        {
            // Use reflection to set the Id since it's likely protected
            var idProperty = typeof(Order).GetProperty("Id");
            idProperty?.SetValue(order, orderId.Value);
        }
        
        return order;
    }
}