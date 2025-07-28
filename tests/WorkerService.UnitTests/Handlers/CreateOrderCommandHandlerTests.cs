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

public class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<CreateOrderCommandHandler>> _mockLogger;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<CreateOrderCommandHandler>>();
        
        _handler = new CreateOrderCommandHandler(
            _mockOrderRepository.Object,
            _mockPublishEndpoint.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateOrderAndReturnResult()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto>
            {
                new("PROD001", 2, 10.00m),
                new("PROD002", 1, 15.50m)
            });

        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Order>());

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be("CUST001");
        result.TotalAmount.Should().Be(35.50m); // (2 * 10.00) + (1 * 15.50)
        result.OrderId.Should().NotBeEmpty();
        result.OrderDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify repository interactions
        _mockOrderRepository.Verify(r => r.AddAsync(It.IsAny<Order>(), cancellationToken), Times.Once);
        _mockOrderRepository.Verify(r => r.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishDomainEvents()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Order>());

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<IDomainEvent>(), cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");

        // Verify no domain events were published when operation fails
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<IDomainEvent>(), cancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrows_ShouldPropagateException()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Save failed");

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Order>());

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(command, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Save failed");
    }

    [Fact]
    public async Task Handle_WhenPublishThrows_ShouldPropagateException()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Message bus failed");

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Order>());

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
    public async Task Handle_ShouldMapCommandToEntityCorrectly()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto>
            {
                new("PROD001", 3, 12.99m),
                new("PROD002", 2, 5.75m)
            });

        var cancellationToken = CancellationToken.None;
        Order? capturedOrder = null;

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .Callback<Order, CancellationToken>((order, ct) => capturedOrder = order)
            .ReturnsAsync(It.IsAny<Order>());

        _mockOrderRepository
            .Setup(r => r.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        capturedOrder.Should().NotBeNull();
        capturedOrder!.CustomerId.Should().Be("CUST001");
        capturedOrder.Items.Should().HaveCount(2);
        capturedOrder.Items.Should().Contain(item => 
            item.ProductId == "PROD001" && 
            item.Quantity == 3 && 
            item.UnitPrice.Amount == 12.99m);
        capturedOrder.Items.Should().Contain(item => 
            item.ProductId == "PROD002" && 
            item.Quantity == 2 && 
            item.UnitPrice.Amount == 5.75m);
        capturedOrder.TotalAmount.Amount.Should().Be(50.47m); // (3 * 12.99) + (2 * 5.75)
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), cancellationToken))
            .ReturnsAsync(It.IsAny<Order>());

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
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Creating order for customer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("created for customer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}