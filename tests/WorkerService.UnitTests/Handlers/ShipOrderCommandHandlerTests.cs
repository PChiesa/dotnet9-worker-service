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

public class ShipOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<ShipOrderCommandHandler>> _mockLogger;
    private readonly ShipOrderCommandHandler _handler;

    public ShipOrderCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<ShipOrderCommandHandler>>();
        _handler = new ShipOrderCommandHandler(
            _mockOrderRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidOrder_ShouldShipOrderAndReturnTrue()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var trackingNumber = "TRACK123456";
        var command = new ShipOrderCommand(orderId, trackingNumber);
        var order = CreatePaidOrder(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);
        order.TrackingNumber.Should().Be(trackingNumber);
        
        _mockOrderRepository.Verify(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ShouldReturnFalse()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new ShipOrderCommand(orderId, "TRACK123456");

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
    public async Task Handle_InvalidOrderState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new ShipOrderCommand(orderId, "TRACK123456");
        var order = CreateValidatedOrder(orderId); // Not paid, so shipping should fail

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only paid orders can be marked as shipped");

        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyTrackingNumber_ShouldThrowArgumentException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new ShipOrderCommand(orderId, "");
        var order = CreatePaidOrder(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Tracking number cannot be empty*");

        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Order CreatePaidOrder(Guid orderId)
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
        order.ProcessPayment(); // Make it paid so it can be shipped
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
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
        
        order.ValidateOrder(); // Only validated, not paid
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }
}