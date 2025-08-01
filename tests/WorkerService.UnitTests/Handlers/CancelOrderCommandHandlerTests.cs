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

public class CancelOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<CancelOrderCommandHandler>> _mockLogger;
    private readonly CancelOrderCommandHandler _handler;

    public CancelOrderCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<CancelOrderCommandHandler>>();
        _handler = new CancelOrderCommandHandler(
            _mockOrderRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidOrderWithReason_ShouldCancelOrderAndReturnTrue()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var reason = "Customer requested cancellation";
        var command = new CancelOrderCommand(orderId, reason);
        var order = CreateValidatedOrder(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        
        _mockOrderRepository.Verify(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidOrderWithoutReason_ShouldCancelOrderAndReturnTrue()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId);
        var order = CreateValidatedOrder(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        
        _mockOrderRepository.Verify(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ShouldReturnFalse()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        
        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeliveredOrder_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId);
        var order = CreateDeliveredOrder(orderId); // Delivered orders cannot be cancelled

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot cancel delivered or already cancelled orders");

        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyCancelledOrder_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId);
        var order = CreateCancelledOrder(orderId); // Already cancelled orders cannot be cancelled again

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot cancel delivered or already cancelled orders");

        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Order CreateValidatedOrder(Guid orderId)
    {
        var items = new List<OrderItem>
        {
            new("PROD001", 1, new Money(10.00m))
        };
        var order = new Order("CUST001", items);
        
        // Use reflection to set the ID since it's read-only
        var idProperty = typeof(Order).GetProperty("Id");
        idProperty?.SetValue(order, orderId);
        
        order.ValidateOrder(); // Validated orders can be cancelled
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }

    private static Order CreateDeliveredOrder(Guid orderId)
    {
        var items = new List<OrderItem>
        {
            new("PROD001", 1, new Money(10.00m))
        };
        var order = new Order("CUST001", items);
        
        // Use reflection to set the ID since it's read-only
        var idProperty = typeof(Order).GetProperty("Id");
        idProperty?.SetValue(order, orderId);
        
        order.ValidateOrder();
        order.ProcessPayment();
        order.MarkAsShipped("TRACK123456");
        order.MarkAsDelivered(); // Make it delivered so it cannot be cancelled
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }

    private static Order CreateCancelledOrder(Guid orderId)
    {
        var items = new List<OrderItem>
        {
            new("PROD001", 1, new Money(10.00m))
        };
        var order = new Order("CUST001", items);
        
        // Use reflection to set the ID since it's read-only
        var idProperty = typeof(Order).GetProperty("Id");
        idProperty?.SetValue(order, orderId);
        
        order.ValidateOrder();
        order.Cancel("Already cancelled"); // Make it cancelled so it cannot be cancelled again
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }
}