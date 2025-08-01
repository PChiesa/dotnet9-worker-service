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

public class MarkOrderDeliveredCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<MarkOrderDeliveredCommandHandler>> _mockLogger;
    private readonly MarkOrderDeliveredCommandHandler _handler;

    public MarkOrderDeliveredCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<MarkOrderDeliveredCommandHandler>>();
        _handler = new MarkOrderDeliveredCommandHandler(
            _mockOrderRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidOrder_ShouldMarkAsDeliveredAndReturnTrue()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new MarkOrderDeliveredCommand(orderId);
        var order = CreateShippedOrder(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
        
        _mockOrderRepository.Verify(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ShouldReturnFalse()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new MarkOrderDeliveredCommand(orderId);

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
        var command = new MarkOrderDeliveredCommand(orderId);
        var order = CreatePaidOrder(orderId); // Not shipped, so delivery should fail

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only shipped orders can be marked as delivered");

        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Order CreateShippedOrder(Guid orderId)
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
        order.MarkAsShipped("TRACK123456"); // Make it shipped so it can be delivered
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
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
        order.ProcessPayment(); // Only paid, not shipped
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }
}