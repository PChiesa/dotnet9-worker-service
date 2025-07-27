using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkerService.Domain.Entities;
using WorkerService.Infrastructure.Data;

namespace WorkerService.IntegrationTests.Utilities;

public class DatabaseAssertions
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseAssertions(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Order existence assertions
    public async Task<Order> AssertOrderExistsAsync(Guid orderId)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        order.Should().NotBeNull($"Order with ID {orderId} should exist in the database");
        return order!;
    }

    public async Task AssertOrderDoesNotExistAsync(Guid orderId)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        order.Should().BeNull($"Order with ID {orderId} should not exist in the database");
    }

    // Order count assertions
    public async Task AssertOrderCountAsync(int expectedCount)
    {
        var actualCount = await _dbContext.Orders.CountAsync();
        actualCount.Should().Be(expectedCount, 
            $"Database should contain exactly {expectedCount} orders");
    }

    public async Task AssertOrderCountGreaterThanAsync(int minimumCount)
    {
        var actualCount = await _dbContext.Orders.CountAsync();
        actualCount.Should().BeGreaterThan(minimumCount,
            $"Database should contain more than {minimumCount} orders");
    }

    // Order status assertions
    public async Task AssertOrderStatusAsync(Guid orderId, OrderStatus expectedStatus)
    {
        var order = await AssertOrderExistsAsync(orderId);
        order.Status.Should().Be(expectedStatus,
            $"Order {orderId} should have status {expectedStatus}");
    }

    public async Task AssertAllOrdersHaveStatusAsync(OrderStatus expectedStatus)
    {
        var orders = await _dbContext.Orders.ToListAsync();
        orders.Should().OnlyContain(o => o.Status == expectedStatus,
            $"All orders should have status {expectedStatus}");
    }

    public async Task AssertOrdersWithStatusCountAsync(OrderStatus status, int expectedCount)
    {
        var count = await _dbContext.Orders
            .CountAsync(o => o.Status == status);
        
        count.Should().Be(expectedCount,
            $"There should be exactly {expectedCount} orders with status {status}");
    }

    // Order property assertions
    public async Task AssertOrderPropertiesAsync(Guid orderId, Action<Order> assertions)
    {
        var order = await AssertOrderExistsAsync(orderId);
        assertions(order);
    }

    public async Task AssertOrderMatchesCommandAsync(Guid orderId, WorkerService.Application.Commands.CreateOrderCommand command)
    {
        var order = await AssertOrderExistsAsync(orderId);
        
        order.CustomerId.Should().Be(command.CustomerId);
        order.Items.Should().HaveCount(command.Items.Count);
        
        var expectedTotal = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        order.TotalAmount.Amount.Should().Be(expectedTotal);
        
        // Verify items match
        foreach (var commandItem in command.Items)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.ProductId == commandItem.ProductId);
            orderItem.Should().NotBeNull($"Product {commandItem.ProductId} should exist in order");
            orderItem!.Quantity.Should().Be(commandItem.Quantity);
            orderItem.UnitPrice.Amount.Should().Be(commandItem.UnitPrice);
        }
    }

    // Order timeline assertions
    public async Task AssertOrderCreatedWithinTimeframeAsync(Guid orderId, TimeSpan timeframe)
    {
        var order = await AssertOrderExistsAsync(orderId);
        var timeSinceCreation = DateTime.UtcNow - order.CreatedAt;
        
        timeSinceCreation.Should().BeLessThan(timeframe,
            $"Order {orderId} should have been created within {timeframe}");
    }

    public async Task AssertOrdersCreatedInOrderAsync(params Guid[] orderIds)
    {
        var orders = new List<Order>();
        foreach (var id in orderIds)
        {
            var order = await AssertOrderExistsAsync(id);
            orders.Add(order);
        }

        for (int i = 1; i < orders.Count; i++)
        {
            orders[i].CreatedAt.Should().BeOnOrAfter(orders[i - 1].CreatedAt,
                $"Order {orderIds[i]} should have been created after order {orderIds[i - 1]}");
        }
    }

    // Customer-based assertions
    public async Task AssertCustomerOrderCountAsync(string customerId, int expectedCount)
    {
        var count = await _dbContext.Orders
            .CountAsync(o => o.CustomerId == customerId);
        
        count.Should().Be(expectedCount,
            $"Customer '{customerId}' should have exactly {expectedCount} orders");
    }

    public async Task<List<Order>> GetCustomerOrdersAsync(string customerId)
    {
        return await _dbContext.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    // Product-based assertions
    public async Task AssertProductOrderCountAsync(string productId, int expectedCount)
    {
        var count = await _dbContext.Orders
            .SelectMany(o => o.Items)
            .CountAsync(i => i.ProductId == productId);
        
        count.Should().Be(expectedCount,
            $"Product '{productId}' should have exactly {expectedCount} order items");
    }

    // Total amount assertions
    public async Task AssertTotalOrderValueAsync(decimal expectedTotal)
    {
        var actualTotal = await _dbContext.Orders
            .SumAsync(o => o.TotalAmount.Amount);
        
        actualTotal.Should().Be(expectedTotal,
            $"Total value of all orders should be {expectedTotal:C}");
    }

    public async Task AssertTotalOrderValueGreaterThanAsync(decimal minimumTotal)
    {
        var actualTotal = await _dbContext.Orders
            .SumAsync(o => o.TotalAmount.Amount);
        
        actualTotal.Should().BeGreaterThan(minimumTotal,
            $"Total value of all orders should be greater than {minimumTotal:C}");
    }

    // Date range assertions
    public async Task<int> GetOrderCountInDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbContext.Orders
            .CountAsync(o => o.OrderDate >= startDate && o.OrderDate <= endDate);
    }

    public async Task AssertOrdersInDateRangeAsync(DateTime startDate, DateTime endDate, int expectedCount)
    {
        var count = await GetOrderCountInDateRangeAsync(startDate, endDate);
        
        count.Should().Be(expectedCount,
            $"There should be exactly {expectedCount} orders between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");
    }

    // State transition assertions
    public async Task AssertOrderProcessingCompletedAsync(Guid orderId)
    {
        var order = await AssertOrderExistsAsync(orderId);
        
        order.Status.Should().BeOneOf(OrderStatus.Delivered, OrderStatus.Paid, OrderStatus.Shipped);
        
        order.UpdatedAt.Should().BeOnOrAfter(order.CreatedAt,
            $"Order {orderId} UpdatedAt should be after CreatedAt");
    }

    // Bulk operation assertions
    public async Task AssertAllOrdersProcessedAsync()
    {
        var unprocessedCount = await _dbContext.Orders
            .CountAsync(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);
        
        unprocessedCount.Should().Be(0,
            "All orders should be either Delivered or Cancelled");
    }

    // Performance assertions
    public async Task<TimeSpan> GetAverageProcessingTimeAsync()
    {
        var processedOrders = await _dbContext.Orders
            .Where(o => o.Status != OrderStatus.Pending)
            .ToListAsync();

        if (!processedOrders.Any())
            return TimeSpan.Zero;

        var totalProcessingTime = processedOrders
            .Sum(o => (o.UpdatedAt - o.CreatedAt).TotalMilliseconds);

        return TimeSpan.FromMilliseconds(totalProcessingTime / processedOrders.Count);
    }

    public async Task AssertAverageProcessingTimeLessThanAsync(TimeSpan maxTime)
    {
        var avgTime = await GetAverageProcessingTimeAsync();
        
        avgTime.Should().BeLessThan(maxTime,
            $"Average order processing time should be less than {maxTime}");
    }

    // Helper method to get all orders for debugging
    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _dbContext.Orders
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    // Cleanup helper
    public async Task ClearAllOrdersAsync()
    {
        _dbContext.Orders.RemoveRange(_dbContext.Orders);
        await _dbContext.SaveChangesAsync();
    }
}