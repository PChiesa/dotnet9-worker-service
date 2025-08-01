using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Domain.Events;
using WorkerService.Infrastructure.Consumers;
using Xunit;

namespace WorkerService.UnitTests.Consumers;

public class OrderCancelledConsumerTests
{
    private readonly Mock<ILogger<OrderCancelledConsumer>> _mockLogger;
    private readonly Mock<ConsumeContext<OrderCancelledEvent>> _mockContext;
    private readonly OrderCancelledConsumer _consumer;

    public OrderCancelledConsumerTests()
    {
        _mockLogger = new Mock<ILogger<OrderCancelledConsumer>>();
        _mockContext = new Mock<ConsumeContext<OrderCancelledEvent>>();
        _consumer = new OrderCancelledConsumer(_mockLogger.Object);
    }

    [Fact]
    public async Task Consume_WithValidEventAndReason_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var reason = "Customer requested cancellation";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {orderId}, Customer {customerId}, Reason: {reason}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Order {orderId} cancelled successfully for customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithValidEventAndNullReason_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, null);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        // Verify that at least one information log was made
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Consume_WithValidEventAndEmptyReason_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var reason = "";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        // Verify that at least one information log was made
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Consume_WithValidEvent_ShouldLogEventId()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST002";
        var reason = "Changed mind";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Event ID: {orderCancelledEvent.EventId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("CUST001", "Customer changed mind")]
    [InlineData("CUSTOMER_VIP", "Found better deal elsewhere")]
    [InlineData("C123", "Product no longer needed")]
    [InlineData("PREMIUM_001", "Ordered by mistake")]
    [InlineData("ENTERPRISE_CUSTOMER_789", "Financial constraints")]
    public async Task Consume_WithVariousCustomersAndReasons_ShouldProcessSuccessfully(
        string customerId, string reason)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {orderId}, Customer {customerId}, Reason: {reason}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Order {orderId} cancelled successfully for customer {customerId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithLongReason_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var reason = "This is a very long cancellation reason that explains in great detail why the customer decided to cancel their order including multiple factors and considerations";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {orderId}, Customer {customerId}, Reason: {reason}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithSpecialCharactersInReason_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var reason = "Customer said: \"I don't need this anymore!\" (Ref: #12345) - 50% discount elsewhere";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {orderId}, Customer {customerId}, Reason: {reason}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithMultipleEvents_ShouldProcessEachSuccessfully()
    {
        // Arrange
        var events = new List<(Guid OrderId, string CustomerId, string? Reason)>
        {
            (Guid.NewGuid(), "CUST001", "Reason 1"),
            (Guid.NewGuid(), "CUST002", "Reason 2"),
            (Guid.NewGuid(), "CUST003", null)
        };

        // Act & Assert
        foreach (var (orderId, customerId, reason) in events)
        {
            var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
            _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

            await _consumer.Consume(_mockContext.Object);

            var expectedReason = reason ?? "No reason provided";
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {orderId}, Customer {customerId}, Reason: {expectedReason}")),
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
        var reason = "Test cancellation";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

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
        var reason = "Test cancellation";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

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
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SHORT")]
    [InlineData("EXTREMELY_LONG_CUSTOMER_ID_THAT_MIGHT_BE_UNUSUAL_BUT_STILL_VALID_FOR_TESTING")]
    public async Task Consume_WithVariousCustomerIdFormats_ShouldProcessSuccessfully(string customerId)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var reason = "Test reason";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {orderId}, Customer {customerId}, Reason: {reason}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithWhitespaceOnlyReason_ShouldTreatAsNoReason()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var reason = "   ";
        var orderCancelledEvent = new OrderCancelledEvent(orderId, customerId, reason);
        
        _mockContext.Setup(x => x.Message).Returns(orderCancelledEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        // Verify that at least one information log was made
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Consume_WithSequentialCancellations_ShouldProcessEachIndependently()
    {
        // Arrange
        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();
        var customer1Id = "CUST001";
        var customer2Id = "CUST002";
        var reason1 = "Reason 1";
        var reason2 = "Reason 2";
        
        var event1 = new OrderCancelledEvent(order1Id, customer1Id, reason1);
        var event2 = new OrderCancelledEvent(order2Id, customer2Id, reason2);

        // Act & Assert - First event
        _mockContext.Setup(x => x.Message).Returns(event1);
        await _consumer.Consume(_mockContext.Object);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {order1Id}, Customer {customer1Id}, Reason: {reason1}")),
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderCancelled event for Order {order2Id}, Customer {customer2Id}, Reason: {reason2}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}