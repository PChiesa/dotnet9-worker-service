using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Application.Commands;
using WorkerService.Application.Handlers;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Events;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Handlers;

public class UpdateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<UpdateOrderCommandHandler>> _mockLogger;
    private readonly UpdateOrderCommandHandler _handler;

    public UpdateOrderCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<UpdateOrderCommandHandler>>();
        
        _handler = new UpdateOrderCommandHandler(
            _mockOrderRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingOrder_ShouldUpdateAndReturnResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST002", // Different customer ID
            new List<OrderItemDto>
            {
                new("PROD003", 3, 12.00m),
                new("PROD004", 1, 8.50m)
            });

        var existingOrder = CreateTestOrder("CUST001", orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
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
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.CustomerId.Should().Be("CUST002");
        result.TotalAmount.Should().Be(44.50m); // (3 * 12.00) + (1 * 8.50)
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify repository interactions
        _mockOrderRepository.Verify(r => r.GetOrderWithItemsAsync(orderId, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.UpdateAsync(existingOrder, cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().BeNull();

        // Verify no update operations were called
        _mockOrderRepository.Verify(r => r.UpdateAsync(It.IsAny<Order>(), cancellationToken), Times.Never);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPublishDomainEvents()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var existingOrder = CreateTestOrder("CUST001", orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert - Verify that domain events were published
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<IDomainEvent>(), cancellationToken),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsOnGet_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
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
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var existingOrder = CreateTestOrder("CUST001", orderId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Update failed");

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
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
    public async Task Handle_WhenPublishThrows_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var existingOrder = CreateTestOrder("CUST001", orderId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Message bus failed");

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync(existingOrder);

        _mockOrderRepository
            .Setup(r => r.UpdateAsync(existingOrder, cancellationToken))
            .Returns(Task.CompletedTask);

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        _mockPublishEndpoint
            .Setup(p => p.Publish(It.IsAny<IDomainEvent>(), cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Message bus failed");
    }

    [Fact]
    public async Task Handle_ShouldUpdateOrderPropertiesCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "UPDATED_CUSTOMER",
            new List<OrderItemDto>
            {
                new("NEW_PROD1", 5, 7.99m),
                new("NEW_PROD2", 2, 13.25m)
            });

        var existingOrder = CreateTestOrder("ORIGINAL_CUSTOMER", orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
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
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be("UPDATED_CUSTOMER");
        result.TotalAmount.Should().Be(66.45m); // (5 * 7.99) + (2 * 13.25)
        
        // Verify the order entity was updated correctly
        existingOrder.CustomerId.Should().Be("UPDATED_CUSTOMER");
        existingOrder.Items.Should().HaveCount(2);
        existingOrder.Items.Should().Contain(item => 
            item.ProductId == "NEW_PROD1" && 
            item.Quantity == 5 && 
            item.UnitPrice.Amount == 7.99m);
        existingOrder.Items.Should().Contain(item => 
            item.ProductId == "NEW_PROD2" && 
            item.Quantity == 2 && 
            item.UnitPrice.Amount == 13.25m);
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var existingOrder = CreateTestOrder("CUST001", orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
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
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Updating order")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("updated for customer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldLogWarning()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not found for update")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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