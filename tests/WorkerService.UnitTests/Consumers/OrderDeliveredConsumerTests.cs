using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Domain.Events;
using WorkerService.Infrastructure.Consumers;
using Xunit;

namespace WorkerService.UnitTests.Consumers;

public class OrderDeliveredConsumerTests
{
    private readonly Mock<ILogger<OrderDeliveredConsumer>> _mockLogger;
    private readonly Mock<ConsumeContext<OrderDeliveredEvent>> _mockContext;
    private readonly OrderDeliveredConsumer _consumer;

    public OrderDeliveredConsumerTests()
    {
        _mockLogger = new Mock<ILogger<OrderDeliveredConsumer>>();
        _mockContext = new Mock<ConsumeContext<OrderDeliveredEvent>>();
        _consumer = new OrderDeliveredConsumer(_mockLogger.Object);
    }

    [Fact]
    public async Task Consume_WithValidEvent_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Order {orderId} delivered successfully to customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithValidEvent_ShouldLogEventId()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST002";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Event ID: {orderDeliveredEvent.EventId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("CUST001")]
    [InlineData("CUSTOMER_VIP")]
    [InlineData("C123")]
    [InlineData("PREMIUM_001")]
    [InlineData("ENTERPRISE_CUSTOMER_789")]
    public async Task Consume_WithVariousCustomerIds_ShouldProcessSuccessfully(string customerId)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Order {orderId} delivered successfully to customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithEmptyCustomerId_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithLongCustomerId_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "VERY_LONG_CUSTOMER_ID_THAT_MIGHT_BE_GENERATED_BY_EXTERNAL_SYSTEM_123456789";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithSpecialCharactersInCustomerId_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST-001_VIP@PREMIUM.COM";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithMultipleEvents_ShouldProcessEachSuccessfully()
    {
        // Arrange
        var events = new List<(Guid OrderId, string CustomerId)>
        {
            (Guid.NewGuid(), "CUST001"),
            (Guid.NewGuid(), "CUST002"),
            (Guid.NewGuid(), "CUST003")
        };

        // Act & Assert
        foreach (var (orderId, customerId) in events)
        {
            var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
            _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

            await _consumer.Consume(_mockContext.Object);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Consume_ShouldCompleteWithoutException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        var act = async () => await _consumer.Consume(_mockContext.Object);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_ShouldLogBothProcessingAndSuccessMessages()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        // Should log exactly 2 information messages
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));

        // Should not log any errors
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("SHORT")]
    [InlineData("EXTREMELY_LONG_CUSTOMER_ID_THAT_MIGHT_BE_UNUSUAL_BUT_STILL_VALID_FOR_TESTING")]
    public async Task Consume_WithVariousCustomerIdFormats_ShouldProcessSuccessfully(string customerId)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderDeliveredEvent = new OrderDeliveredEvent(orderId, customerId);
        
        _mockContext.Setup(x => x.Message).Returns(orderDeliveredEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {orderId}, Customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithSequentialOrders_ShouldProcessEachIndependently()
    {
        // Arrange
        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();
        var customer1Id = "CUST001";
        var customer2Id = "CUST002";
        
        var event1 = new OrderDeliveredEvent(order1Id, customer1Id);
        var event2 = new OrderDeliveredEvent(order2Id, customer2Id);

        // Act & Assert - First event
        _mockContext.Setup(x => x.Message).Returns(event1);
        await _consumer.Consume(_mockContext.Object);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {order1Id}, Customer {customer1Id}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Act & Assert - Second event
        _mockContext.Setup(x => x.Message).Returns(event2);
        await _consumer.Consume(_mockContext.Object);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderDelivered event for Order {order2Id}, Customer {customer2Id}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}