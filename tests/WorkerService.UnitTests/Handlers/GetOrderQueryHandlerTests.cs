using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Handlers;
using WorkerService.Application.Queries;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Handlers;

public class GetOrderQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<ILogger<GetOrderQueryHandler>> _mockLogger;
    private readonly GetOrderQueryHandler _handler;

    public GetOrderQueryHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockLogger = new Mock<ILogger<GetOrderQueryHandler>>();
        
        _handler = new GetOrderQueryHandler(
            _mockOrderRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingOrder_ShouldReturnOrderResponseDto()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderQuery(orderId);
        var cancellationToken = CancellationToken.None;

        var orderItems = new List<OrderItem>
        {
            new("PROD001", 2, new Money(10.00m)),
            new("PROD002", 1, new Money(15.50m))
        };
        var order = new Order("CUST001", orderItems);

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id); // Use the actual order ID, not the query ID
        result.CustomerId.Should().Be("CUST001");
        result.Status.Should().Be("Pending");
        result.TotalAmount.Should().Be(35.50m);
        result.Items.Should().HaveCount(2);
        
        var firstItem = result.Items.First();
        firstItem.ProductId.Should().Be("PROD001");
        firstItem.Quantity.Should().Be(2);
        firstItem.UnitPrice.Should().Be(10.00m);
        firstItem.TotalPrice.Should().Be(20.00m);
    }

    [Fact]
    public async Task Handle_WithNonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderQuery(orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderQuery(orderId);
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(query, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryWithCorrectParameters()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderQuery(orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockOrderRepository.Verify(
            r => r.GetOrderWithItemsAsync(orderId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderQuery(orderId);
        var cancellationToken = CancellationToken.None;

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync((Order?)null);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Retrieving order")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithComplexOrder_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderQuery(orderId);
        var cancellationToken = CancellationToken.None;

        var orderItems = new List<OrderItem>
        {
            new("PROD001", 3, new Money(12.99m)),
            new("PROD002", 1, new Money(25.00m)),
            new("PROD003", 2, new Money(8.50m))
        };
        var order = new Order("CUST999", orderItems);
        
        // Validate the order to change status
        order.ValidateOrder();

        _mockOrderRepository
            .Setup(r => r.GetOrderWithItemsAsync(orderId, cancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.CustomerId.Should().Be("CUST999");
        result.Status.Should().Be("Validated");
        result.TotalAmount.Should().Be(80.97m); // (3 * 12.99) + (1 * 25.00) + (2 * 8.50) = 38.97 + 25.00 + 17.00
        result.Items.Should().HaveCount(3);
        
        // Verify item mappings
        var items = result.Items.ToList();
        items[0].ProductId.Should().Be("PROD001");
        items[0].Quantity.Should().Be(3);
        items[0].UnitPrice.Should().Be(12.99m);
        items[0].TotalPrice.Should().Be(38.97m);
        
        items[1].ProductId.Should().Be("PROD002");
        items[1].Quantity.Should().Be(1);
        items[1].UnitPrice.Should().Be(25.00m);
        items[1].TotalPrice.Should().Be(25.00m);
        
        items[2].ProductId.Should().Be("PROD003");
        items[2].Quantity.Should().Be(2);
        items[2].UnitPrice.Should().Be(8.50m);
        items[2].TotalPrice.Should().Be(17.00m);
    }
}