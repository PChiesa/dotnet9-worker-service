using FluentAssertions;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Extensions;

public class MappingExtensionsTests
{
    #region ToResponseDto Tests

    [Fact]
    public void ToResponseDto_WithValidOrder_ShouldMapCorrectly()
    {
        // Arrange
        var orderItems = new List<OrderItem>
        {
            new("PROD001", 2, new Money(10.50m)),
            new("PROD002", 1, new Money(25.00m))
        };
        var order = new Order("CUST001", orderItems);
        
        // Act
        var result = order.ToResponseDto();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(order.Id);
        result.CustomerId.Should().Be("CUST001");
        result.OrderDate.Should().Be(order.OrderDate);
        result.Status.Should().Be(order.Status.ToString());
        result.TotalAmount.Should().Be(46.00m); // (2 * 10.50) + (1 * 25.00)
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public void ToResponseDto_WithNullOrder_ShouldThrowArgumentNullException()
    {
        // Arrange
        Order? order = null;

        // Act & Assert
        var action = () => order!.ToResponseDto();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToResponseDto_WithOrderWithMultipleItems_ShouldMapAllItems()
    {
        // Arrange
        var orderItems = new List<OrderItem>
        {
            new("PROD001", 3, new Money(7.99m)),
            new("PROD002", 1, new Money(15.50m)),
            new("PROD003", 2, new Money(12.25m))
        };
        var order = new Order("CUST999", orderItems);

        // Act
        var result = order.ToResponseDto();

        // Assert
        result.Items.Should().HaveCount(3);
        var items = result.Items.ToList();
        
        items[0].ProductId.Should().Be("PROD001");
        items[0].Quantity.Should().Be(3);
        items[0].UnitPrice.Should().Be(7.99m);
        items[0].TotalPrice.Should().Be(23.97m);
        
        items[1].ProductId.Should().Be("PROD002");
        items[1].Quantity.Should().Be(1);
        items[1].UnitPrice.Should().Be(15.50m);
        items[1].TotalPrice.Should().Be(15.50m);
        
        items[2].ProductId.Should().Be("PROD003");
        items[2].Quantity.Should().Be(2);
        items[2].UnitPrice.Should().Be(12.25m);
        items[2].TotalPrice.Should().Be(24.50m);
    }

    [Fact]
    public void ToResponseDto_WithDifferentOrderStatus_ShouldMapStatusCorrectly()
    {
        // Arrange
        var orderItems = new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) };
        var order = new Order("CUST001", orderItems);
        order.ValidateOrder(); // Change status to Validated

        // Act
        var result = order.ToResponseDto();

        // Assert
        result.Status.Should().Be("Validated");
    }

    #endregion

    #region ToResponseDto (OrderItem) Tests

    [Fact]
    public void OrderItem_ToResponseDto_WithValidOrderItem_ShouldMapCorrectly()
    {
        // Arrange
        var orderItem = new OrderItem("PROD123", 5, new Money(8.75m));

        // Act
        var result = orderItem.ToResponseDto();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(orderItem.Id);
        result.ProductId.Should().Be("PROD123");
        result.Quantity.Should().Be(5);
        result.UnitPrice.Should().Be(8.75m);
        result.TotalPrice.Should().Be(43.75m); // 5 * 8.75
    }

    [Fact]
    public void OrderItem_ToResponseDto_WithNullOrderItem_ShouldThrowArgumentNullException()
    {
        // Arrange
        OrderItem? orderItem = null;

        // Act & Assert
        var action = () => orderItem!.ToResponseDto();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OrderItem_ToResponseDto_WithSingleQuantity_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var orderItem = new OrderItem("PROD001", 1, new Money(10.00m));

        // Act
        var result = orderItem.ToResponseDto();

        // Assert
        result.Quantity.Should().Be(1);
        result.TotalPrice.Should().Be(10.00m);
    }

    #endregion

    #region ToEntity Tests

    [Fact]
    public void ToEntity_WithValidCreateCommand_ShouldCreateOrderCorrectly()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto>
            {
                new("PROD001", 2, 15.99m),
                new("PROD002", 1, 8.50m)
            });

        // Act
        var result = command.ToEntity();

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be("CUST001");
        result.Items.Should().HaveCount(2);
        result.TotalAmount.Amount.Should().Be(40.48m); // (2 * 15.99) + (1 * 8.50)
        result.Status.Should().Be(OrderStatus.Pending);
        
        var items = result.Items.ToList();
        items[0].ProductId.Should().Be("PROD001");
        items[0].Quantity.Should().Be(2);
        items[0].UnitPrice.Amount.Should().Be(15.99m);
        
        items[1].ProductId.Should().Be("PROD002");
        items[1].Quantity.Should().Be(1);
        items[1].UnitPrice.Amount.Should().Be(8.50m);
    }

    [Fact]
    public void ToEntity_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        CreateOrderCommand? command = null;

        // Act & Assert
        var action = () => command!.ToEntity();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToEntity_WithNullItems_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateOrderCommand("CUST001", null!);

        // Act & Assert
        var action = () => command.ToEntity();
        action.Should().Throw<ArgumentException>()
            .WithMessage("Order items cannot be null*");
    }

    [Fact]
    public void ToEntity_WithEmptyItems_ShouldThrowFromDomainLogic()
    {
        // Arrange
        var command = new CreateOrderCommand("CUST001", new List<OrderItemDto>());

        // Act & Assert
        var action = () => command.ToEntity();
        action.Should().Throw<ArgumentException>()
            .WithMessage("Order must contain at least one item*");
    }

    [Fact]
    public void ToEntity_WithValidSingleItem_ShouldCreateOrderCorrectly()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST999",
            new List<OrderItemDto> { new("PROD123", 10, 2.99m) });

        // Act
        var result = command.ToEntity();

        // Assert
        result.CustomerId.Should().Be("CUST999");
        result.Items.Should().HaveCount(1);
        result.TotalAmount.Amount.Should().Be(29.90m); // 10 * 2.99
        
        var item = result.Items.First();
        item.ProductId.Should().Be("PROD123");
        item.Quantity.Should().Be(10);
        item.UnitPrice.Amount.Should().Be(2.99m);
    }

    #endregion

    #region ToCreateResult Tests

    [Fact]
    public void ToCreateResult_WithValidOrder_ShouldMapCorrectly()
    {
        // Arrange
        var orderItems = new List<OrderItem> { new("PROD001", 3, new Money(12.50m)) };
        var order = new Order("CUST001", orderItems);

        // Act
        var result = order.ToCreateResult();

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.CustomerId.Should().Be("CUST001");
        result.TotalAmount.Should().Be(37.50m); // 3 * 12.50
        result.OrderDate.Should().Be(order.OrderDate);
    }

    [Fact]
    public void ToCreateResult_WithNullOrder_ShouldThrowArgumentNullException()
    {
        // Arrange
        Order? order = null;

        // Act & Assert
        var action = () => order!.ToCreateResult();
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ToUpdateResult Tests

    [Fact]
    public void ToUpdateResult_WithValidOrder_ShouldMapCorrectly()
    {
        // Arrange
        var orderItems = new List<OrderItem> { new("PROD001", 2, new Money(20.00m)) };
        var order = new Order("CUST001", orderItems);

        // Act
        var result = order.ToUpdateResult();

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.CustomerId.Should().Be("CUST001");
        result.TotalAmount.Should().Be(40.00m); // 2 * 20.00
        result.UpdatedAt.Should().Be(order.UpdatedAt);
    }

    [Fact]
    public void ToUpdateResult_WithNullOrder_ShouldThrowArgumentNullException()
    {
        // Arrange
        Order? order = null;

        // Act & Assert
        var action = () => order!.ToUpdateResult();
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ToPagedResult Tests

    [Fact]
    public void ToPagedResult_WithValidData_ShouldMapCorrectly()
    {
        // Arrange
        var orders = new List<Order>
        {
            new("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) }),
            new("CUST002", new List<OrderItem> { new("PROD002", 2, new Money(15.00m)) })
        };
        var pagedData = (Orders: orders, TotalCount: 25);
        var pageNumber = 2;
        var pageSize = 10;

        // Act
        var result = pagedData.ToPagedResult(pageNumber, pageSize);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().HaveCount(2);
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.HasNextPage.Should().BeTrue(); // 2 * 10 = 20 < 25
        result.HasPreviousPage.Should().BeTrue(); // 2 > 1
    }

    [Fact]
    public void ToPagedResult_WithFirstPage_ShouldSetPaginationCorrectly()
    {
        // Arrange
        var orders = new List<Order>
        {
            new("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) })
        };
        var pagedData = (Orders: orders, TotalCount: 15);
        var pageNumber = 1;
        var pageSize = 20;

        // Act
        var result = pagedData.ToPagedResult(pageNumber, pageSize);

        // Assert
        result.HasNextPage.Should().BeFalse(); // 1 * 20 = 20 > 15
        result.HasPreviousPage.Should().BeFalse(); // 1 = 1
    }

    [Fact]
    public void ToPagedResult_WithLastPage_ShouldSetPaginationCorrectly()
    {
        // Arrange
        var orders = new List<Order>
        {
            new("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) })
        };
        var pagedData = (Orders: orders, TotalCount: 21);
        var pageNumber = 3;
        var pageSize = 10;

        // Act
        var result = pagedData.ToPagedResult(pageNumber, pageSize);

        // Assert
        result.HasNextPage.Should().BeFalse(); // 3 * 10 = 30 > 21
        result.HasPreviousPage.Should().BeTrue(); // 3 > 1
    }

    [Fact]
    public void ToPagedResult_WithEmptyOrders_ShouldHandleCorrectly()
    {
        // Arrange
        var orders = new List<Order>();
        var pagedData = (Orders: orders, TotalCount: 0);
        var pageNumber = 1;
        var pageSize = 20;

        // Act
        var result = pagedData.ToPagedResult(pageNumber, pageSize);

        // Assert
        result.Orders.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void ToPagedResult_ShouldMapOrdersToResponseDtos()
    {
        // Arrange
        var order1 = new Order("CUST001", new List<OrderItem> { new("PROD001", 1, new Money(10.00m)) });
        var order2 = new Order("CUST002", new List<OrderItem> { new("PROD002", 2, new Money(15.00m)) });
        order2.ValidateOrder(); // Change status
        
        var orders = new List<Order> { order1, order2 };
        var pagedData = (Orders: orders, TotalCount: 2);

        // Act
        var result = pagedData.ToPagedResult(1, 20);

        // Assert
        var ordersList = result.Orders.ToList();
        ordersList.Should().HaveCount(2);
        
        ordersList[0].CustomerId.Should().Be("CUST001");
        ordersList[0].Status.Should().Be("Pending");
        ordersList[0].TotalAmount.Should().Be(10.00m);
        
        ordersList[1].CustomerId.Should().Be("CUST002");
        ordersList[1].Status.Should().Be("Validated");
        ordersList[1].TotalAmount.Should().Be(30.00m); // 2 * 15.00
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Theory]
    [InlineData(1, 1, 1, false, false)] // Single item, single page
    [InlineData(1, 10, 10, false, false)] // Exact page size
    [InlineData(1, 10, 11, true, false)] // One more than page size
    [InlineData(2, 10, 20, false, true)] // Exact two pages
    [InlineData(2, 10, 21, true, true)] // One more than two pages
    public void ToPagedResult_WithVariousPaginationScenarios_ShouldCalculateCorrectly(
        int pageNumber, int pageSize, int totalCount, bool expectedHasNext, bool expectedHasPrev)
    {
        // Arrange
        var orders = new List<Order>();
        var pagedData = (Orders: orders, TotalCount: totalCount);

        // Act
        var result = pagedData.ToPagedResult(pageNumber, pageSize);

        // Assert
        result.HasNextPage.Should().Be(expectedHasNext);
        result.HasPreviousPage.Should().Be(expectedHasPrev);
    }

    [Fact]
    public void ToEntity_WithDecimalPrecision_ShouldPreservePrecision()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto>
            {
                new("PROD001", 3, 10.99m), // Use lower precision to avoid rounding issues
                new("PROD002", 1, 0.01m)   // Low value
            });

        // Act
        var result = command.ToEntity();

        // Assert
        result.TotalAmount.Amount.Should().Be(32.98m); // (3 * 10.99) + (1 * 0.01) = 32.97 + 0.01 = 32.98
        
        var items = result.Items.ToList();
        items[0].UnitPrice.Amount.Should().Be(10.99m);
        items[1].UnitPrice.Amount.Should().Be(0.01m);
    }

    #endregion
}