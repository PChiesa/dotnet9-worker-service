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

public class ProcessPaymentCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<ProcessPaymentCommandHandler>> _mockLogger;
    private readonly ProcessPaymentCommandHandler _handler;

    public ProcessPaymentCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<ProcessPaymentCommandHandler>>();
        _handler = new ProcessPaymentCommandHandler(
            _mockOrderRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidOrder_ShouldProcessPaymentAndReturnTrue()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new ProcessPaymentCommand(orderId);
        var order = CreateValidatedOrder(orderId);

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
        
        _mockOrderRepository.Verify(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ShouldReturnFalse()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new ProcessPaymentCommand(orderId);

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
        var command = new ProcessPaymentCommand(orderId);
        var order = CreatePendingOrder(orderId); // Not validated, so payment processing should fail

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only validated orders can be processed for payment");

        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockOrderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RepositoryException_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new ProcessPaymentCommand(orderId);
        var expectedError = new InvalidOperationException("Database error");

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedError);

        // Act & Assert
        var action = async () => await _handler.Handle(command, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database error");
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
        
        order.ValidateOrder(); // Make it validated so payment can be processed
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }

    private static Order CreatePendingOrder(Guid orderId)
    {
        var items = new List<OrderItem>
        {
            new("PROD001", 1, new Money(10.00m))
        };
        var order = new Order("CUST001", items);
        
        // Use reflection to set the ID since it's read-only
        var idProperty = typeof(Order).GetProperty("Id");
        idProperty?.SetValue(order, orderId);
        
        order.ClearDomainEvents(); // Clear events so we can test new ones
        
        return order;
    }
}