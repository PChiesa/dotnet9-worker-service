using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Domain.Events;
using WorkerService.Infrastructure.Consumers;
using Xunit;

namespace WorkerService.UnitTests.Consumers;

public class OrderPaidConsumerTests
{
    private readonly Mock<ILogger<OrderPaidConsumer>> _mockLogger;
    private readonly Mock<ConsumeContext<OrderPaidEvent>> _mockContext;
    private readonly OrderPaidConsumer _consumer;

    public OrderPaidConsumerTests()
    {
        _mockLogger = new Mock<ILogger<OrderPaidConsumer>>();
        _mockContext = new Mock<ConsumeContext<OrderPaidEvent>>();
        _consumer = new OrderPaidConsumer(_mockLogger.Object);
    }

    [Fact]
    public async Task Consume_WithValidEvent_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 199.99m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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

        // Verify that at least one additional information log was made
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Consume_WithZeroAmount_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 0m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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
    public async Task Consume_WithLargeAmount_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 99999.99m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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
        var amount = 149.99m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Event ID: {orderPaidEvent.EventId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WithDecimalAmount_ShouldProcessSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 25.67m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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

    [Theory]
    [InlineData(0.01)]
    [InlineData(1.00)]
    [InlineData(9.99)]
    [InlineData(100.50)]
    [InlineData(1000.00)]
    public async Task Consume_WithVariousAmounts_ShouldProcessSuccessfully(decimal amount)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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

        // Verify that at least one additional information log was made
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Consume_WithMultipleEvents_ShouldProcessEachSuccessfully()
    {
        // Arrange
        var events = new List<(Guid OrderId, decimal Amount)>
        {
            (Guid.NewGuid(), 50.00m),
            (Guid.NewGuid(), 100.00m),
            (Guid.NewGuid(), 75.50m)
        };

        // Act & Assert
        foreach (var (orderId, amount) in events)
        {
            var orderPaidEvent = new OrderPaidEvent(orderId, amount);
            _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

            await _consumer.Consume(_mockContext.Object);

            // Verify that at least one information log was made for this event
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }

    [Fact]
    public async Task Consume_ShouldCompleteWithoutException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 99.99m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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
        var amount = 199.99m;
        var orderPaidEvent = new OrderPaidEvent(orderId, amount);
        
        _mockContext.Setup(x => x.Message).Returns(orderPaidEvent);

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
}