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

public class GetOrdersQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<ILogger<GetOrdersQueryHandler>> _mockLogger;
    private readonly GetOrdersQueryHandler _handler;

    public GetOrdersQueryHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockLogger = new Mock<ILogger<GetOrdersQueryHandler>>();
        
        _handler = new GetOrdersQueryHandler(
            _mockOrderRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithDefaultParameters_ShouldReturnPagedResults()
    {
        // Arrange
        var query = new GetOrdersQuery();
        var cancellationToken = CancellationToken.None;

        var orders = new List<Order>
        {
            CreateTestOrder("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) }),
            CreateTestOrder("CUST002", new List<OrderItem> { new("PROD002", 2, new Money(15.00m)) })
        };

        var pagedData = (Orders: orders, TotalCount: 25);

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(1, 20, null, cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().HaveCount(2);
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.HasNextPage.Should().BeTrue(); // 25 total > 20 page size
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithCustomParameters_ShouldPassCorrectParametersToRepository()
    {
        // Arrange
        var query = new GetOrdersQuery(PageNumber: 3, PageSize: 10, CustomerId: "CUST123");
        var cancellationToken = CancellationToken.None;

        var pagedData = (Orders: new List<Order>(), TotalCount: 0);

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(3, 10, "CUST123", cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockOrderRepository.Verify(
            r => r.GetPagedAsync(3, 10, "CUST123", cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithFilteredResults_ShouldReturnCorrectPagedData()
    {
        // Arrange
        var query = new GetOrdersQuery(PageNumber: 1, PageSize: 5, CustomerId: "CUST001");
        var cancellationToken = CancellationToken.None;

        var orders = new List<Order>
        {
            CreateTestOrder("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) }),
            CreateTestOrder("CUST001", new List<OrderItem> { new("PROD002", 2, new Money(20.00m)) }),
            CreateTestOrder("CUST001", new List<OrderItem> { new("PROD003", 1, new Money(30.00m)) })
        };

        var pagedData = (Orders: orders, TotalCount: 8);

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(1, 5, "CUST001", cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().HaveCount(3);
        result.TotalCount.Should().Be(8);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(5);
        result.HasNextPage.Should().BeTrue(); // 8 total > 5 page size
        result.HasPreviousPage.Should().BeFalse();
        
        // Verify all returned orders are for the correct customer
        result.Orders.Should().OnlyContain(o => o.CustomerId == "CUST001");
    }

    [Fact]
    public async Task Handle_WithLastPage_ShouldSetPaginationCorrectly()
    {
        // Arrange
        var query = new GetOrdersQuery(PageNumber: 3, PageSize: 10);
        var cancellationToken = CancellationToken.None;

        var orders = new List<Order>
        {
            CreateTestOrder("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) })
        };

        var pagedData = (Orders: orders, TotalCount: 21); // 21 items, page 3 of size 10

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(3, 10, null, cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PageNumber.Should().Be(3);
        result.PageSize.Should().Be(10);
        result.HasNextPage.Should().BeFalse(); // 3 * 10 = 30 > 21 total
        result.HasPreviousPage.Should().BeTrue(); // page 3 > 1
    }

    [Fact]
    public async Task Handle_WithEmptyResults_ShouldReturnEmptyPagedResult()
    {
        // Arrange
        var query = new GetOrdersQuery(PageNumber: 1, PageSize: 20, CustomerId: "NONEXISTENT");
        var cancellationToken = CancellationToken.None;

        var pagedData = (Orders: new List<Order>(), TotalCount: 0);

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(1, 20, "NONEXISTENT", cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var query = new GetOrdersQuery();
        var cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var action = async () => await _handler.Handle(query, cancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task Handle_ShouldLogAppropriateMessages()
    {
        // Arrange
        var query = new GetOrdersQuery(PageNumber: 2, PageSize: 15, CustomerId: "CUST001");
        var cancellationToken = CancellationToken.None;

        var orders = new List<Order>
        {
            CreateTestOrder("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) })
        };
        var pagedData = (Orders: orders, TotalCount: 1);

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(2, 15, "CUST001", cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Retrieving orders") && 
                                            o.ToString()!.Contains("Page: 2") && 
                                            o.ToString()!.Contains("Size: 15") && 
                                            o.ToString()!.Contains("Customer: CUST001")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Retrieved") && 
                                            o.ToString()!.Contains("orders for page")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapOrdersToResponseDtosCorrectly()
    {
        // Arrange
        var query = new GetOrdersQuery(PageNumber: 1, PageSize: 5);
        var cancellationToken = CancellationToken.None;

        var orderItems1 = new List<OrderItem> { new("PROD001", 2, new Money(15.50m)) };
        var orderItems2 = new List<OrderItem> { new("PROD002", 1, new Money(25.00m)) };
        
        var order1 = CreateTestOrder("CUST001", orderItems1);
        var order2 = CreateTestOrder("CUST002", orderItems2);
        order2.ValidateOrder(); // Change status to test mapping

        var orders = new List<Order> { order1, order2 };
        var pagedData = (Orders: orders, TotalCount: 2);

        _mockOrderRepository
            .Setup(r => r.GetPagedAsync(1, 5, null, cancellationToken))
            .ReturnsAsync(pagedData);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().HaveCount(2);

        var ordersList = result.Orders.ToList();
        
        // First order
        ordersList[0].CustomerId.Should().Be("CUST001");
        ordersList[0].Status.Should().Be("Pending");
        ordersList[0].TotalAmount.Should().Be(31.00m); // 2 * 15.50
        ordersList[0].Items.Should().HaveCount(1);
        
        // Second order
        ordersList[1].CustomerId.Should().Be("CUST002");
        ordersList[1].Status.Should().Be("Validated");
        ordersList[1].TotalAmount.Should().Be(25.00m);
        ordersList[1].Items.Should().HaveCount(1);
    }

    private static Order CreateTestOrder(string customerId, List<OrderItem> items)
    {
        return new Order(customerId, items);
    }
}