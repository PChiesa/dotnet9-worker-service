using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Domain.Events;
using WorkerService.Infrastructure.Consumers;
using Xunit;

namespace WorkerService.UnitTests.Consumers;

public class OrderShippedConsumerTests
{
    private readonly Mock<ILogger<OrderShippedConsumer>> _mockLogger;
    private readonly Mock<ConsumeContext<OrderShippedEvent>> _mockContext;
    private readonly OrderShippedConsumer _consumer;

    public OrderShippedConsumerTests()
    {
        _mockLogger = new Mock<ILogger<OrderShippedConsumer>>();
        _mockContext = new Mock<ConsumeContext<OrderShippedEvent>>();
        _consumer = new OrderShippedConsumer(_mockLogger.Object);
    }

    [Fact]
    public async Task Consume_WithValidEvent_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var trackingNumber = "TRACK123456";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Order {orderId} shipped successfully with tracking number {trackingNumber}")),
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
        var trackingNumber = "TRACK789012";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Event ID: {orderShippedEvent.EventId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("CUST001", "1Z999AA1234567890")]
    [InlineData("CUSTOMER_VIP", "FEDEX123456789")]
    [InlineData("C123", "DHL987654321")]
    [InlineData("PREMIUM_001", "UPS1234567890")]
    public async Task Consume_WithVariousCustomersAndTrackingNumbers_ShouldProcessSuccessfully(
        string customerId, string trackingNumber)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Order {orderId} shipped successfully with tracking number {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithLongTrackingNumber_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var trackingNumber = "VERY_LONG_TRACKING_NUMBER_123456789012345678901234567890";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithSpecialCharactersInTrackingNumber_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var trackingNumber = "TRACK-123_456.789@TEST";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithMultipleEvents_ShouldProcessEachSuccessfully()
    {
        // Arrange
        var events = new List<(Guid OrderId, string CustomerId, string TrackingNumber)>
        {
            (Guid.NewGuid(), "CUST001", "TRACK123"),
            (Guid.NewGuid(), "CUST002", "TRACK456"),
            (Guid.NewGuid(), "CUST003", "TRACK789")
        };

        // Act & Assert
        foreach (var (orderId, customerId, trackingNumber) in events)
        {
            var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
            _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

            await _consumer.Consume(_mockContext.Object);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
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
        var trackingNumber = "TRACK123456";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

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
        var trackingNumber = "TRACK123456";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

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
    [InlineData("EXTREMELY_LONG_CUSTOMER_ID_THAT_MIGHT_BE_UNUSUAL_BUT_STILL_VALID")]
    public async Task Consume_WithVariousCustomerIdFormats_ShouldProcessSuccessfully(string customerId)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var trackingNumber = "TRACK123456";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithEmptyTrackingNumber_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = "CUST001";
        var trackingNumber = "";
        var orderShippedEvent = new OrderShippedEvent(orderId, customerId, trackingNumber);
        
        _mockContext.Setup(x => x.Message).Returns(orderShippedEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Processing OrderShipped event for Order {orderId}, Customer {customerId}, Tracking {trackingNumber}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}