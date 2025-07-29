using FluentAssertions;
using WorkerService.Application.Commands;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.IntegrationTests.Shared.Utilities;

/// <summary>
/// Builder pattern for creating test order data with fluent API
/// </summary>
public class OrderTestDataBuilder
{
    private string _customerId = "CUST001";
    private readonly List<OrderItemDto> _items = new();
    private readonly List<OrderItem> _entityItems = new();

    public static OrderTestDataBuilder Create() => new();

    public OrderTestDataBuilder WithCustomerId(string customerId)
    {
        _customerId = customerId;
        return this;
    }

    public OrderTestDataBuilder WithItem(string productId, int quantity, decimal unitPrice)
    {
        _items.Add(new OrderItemDto(productId, quantity, unitPrice));
        _entityItems.Add(new OrderItem(productId, quantity, new Money(unitPrice)));
        return this;
    }

    public OrderTestDataBuilder WithRandomItems(int count = 2)
    {
        var random = new Random();
        for (int i = 0; i < count; i++)
        {
            var productId = $"PROD{random.Next(100, 999)}";
            var quantity = random.Next(1, 5);
            var unitPrice = Math.Round((decimal)(random.NextDouble() * 100), 2);
            
            WithItem(productId, quantity, unitPrice);
        }
        return this;
    }

    public OrderTestDataBuilder WithExpensiveItems()
    {
        return WithItem("LUXURY001", 1, 999.99m)
               .WithItem("LUXURY002", 2, 450.00m);
    }

    public OrderTestDataBuilder WithCheapItems()
    {
        return WithItem("CHEAP001", 10, 0.99m)
               .WithItem("CHEAP002", 5, 1.50m);
    }

    public CreateOrderCommand BuildCreateCommand()
    {
        if (!_items.Any())
        {
            WithRandomItems();
        }
        return new CreateOrderCommand(_customerId, _items);
    }

    public UpdateOrderCommand BuildUpdateCommand(Guid orderId)
    {
        if (!_items.Any())
        {
            WithRandomItems();
        }
        return new UpdateOrderCommand(orderId, _customerId, _items);
    }

    public Order BuildEntity()
    {
        if (!_entityItems.Any())
        {
            WithRandomItems();
        }
        return new Order(_customerId, _entityItems);
    }

    public List<Order> BuildMultipleEntities(int count, string? customerIdPrefix = null)
    {
        var orders = new List<Order>();
        var random = new Random();
        
        for (int i = 0; i < count; i++)
        {
            var customerId = customerIdPrefix != null ? $"{customerIdPrefix}{i:D3}" : $"CUST{random.Next(100, 999)}";
            var builder = Create().WithCustomerId(customerId).WithRandomItems(random.Next(1, 4));
            orders.Add(builder.BuildEntity());
        }
        
        return orders;
    }
}

/// <summary>
/// Static factory methods for common test scenarios
/// </summary>
public static class OrderTestData
{
    public static CreateOrderCommand SimpleCreateCommand() =>
        OrderTestDataBuilder.Create()
            .WithCustomerId("CUST001")
            .WithItem("PROD001", 2, 10.50m)
            .BuildCreateCommand();

    public static CreateOrderCommand ComplexCreateCommand() =>
        OrderTestDataBuilder.Create()
            .WithCustomerId("CUST999")
            .WithItem("PROD001", 3, 15.99m)
            .WithItem("PROD002", 1, 25.00m)
            .WithItem("PROD003", 2, 8.75m)
            .BuildCreateCommand();

    public static CreateOrderCommand ExpensiveOrderCommand() =>
        OrderTestDataBuilder.Create()
            .WithCustomerId("VIP001")
            .WithExpensiveItems()
            .BuildCreateCommand();

    public static CreateOrderCommand CheapOrderCommand() =>
        OrderTestDataBuilder.Create()
            .WithCustomerId("BUDGET001")
            .WithCheapItems()
            .BuildCreateCommand();

    public static UpdateOrderCommand SimpleUpdateCommand(Guid orderId) =>
        OrderTestDataBuilder.Create()
            .WithCustomerId("UPDATED_CUST")
            .WithItem("UPDATED_PROD", 5, 12.99m)
            .BuildUpdateCommand(orderId);

    public static Order SimpleOrder() =>
        OrderTestDataBuilder.Create()
            .WithCustomerId("CUST001")
            .WithItem("PROD001", 1, 20.00m)
            .BuildEntity();

    public static List<Order> MultipleOrders(int count = 5) =>
        OrderTestDataBuilder.Create().BuildMultipleEntities(count);

    public static List<Order> OrdersForCustomer(string customerId, int count = 3)
    {
        var orders = new List<Order>();
        for (int i = 0; i < count; i++)
        {
            var order = OrderTestDataBuilder.Create()
                .WithCustomerId(customerId)
                .WithRandomItems()
                .BuildEntity();
            orders.Add(order);
        }
        return orders;
    }

    /// <summary>
    /// Creates invalid command data for testing validation
    /// </summary>
    public static class Invalid
    {
        public static CreateOrderCommand EmptyCustomerId() =>
            new("", new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        public static CreateOrderCommand NullCustomerId() =>
            new(null!, new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        public static CreateOrderCommand EmptyItems() =>
            new("CUST001", new List<OrderItemDto>());

        public static CreateOrderCommand NullItems() =>
            new("CUST001", null!);

        public static CreateOrderCommand InvalidQuantity() =>
            new("CUST001", new List<OrderItemDto> { new("PROD001", -1, 10.00m) });

        public static CreateOrderCommand InvalidPrice() =>
            new("CUST001", new List<OrderItemDto> { new("PROD001", 1, -10.00m) });

        public static CreateOrderCommand EmptyProductId() =>
            new("CUST001", new List<OrderItemDto> { new("", 1, 10.00m) });
    }
}

/// <summary>
/// Helper class for assertion operations in tests
/// </summary>
public static class OrderTestAssertions
{
    public static void AssertOrderMatchesCommand(Order order, CreateOrderCommand command)
    {
        order.CustomerId.Should().Be(command.CustomerId);
        order.Items.Should().HaveCount(command.Items.Count);
        
        var expectedTotal = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        order.TotalAmount.Amount.Should().Be(expectedTotal);
        
        foreach (var commandItem in command.Items)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.ProductId == commandItem.ProductId);
            orderItem.Should().NotBeNull();
            orderItem!.Quantity.Should().Be(commandItem.Quantity);
            orderItem.UnitPrice.Amount.Should().Be(commandItem.UnitPrice);
        }
    }

    public static void AssertOrderMatchesUpdateCommand(Order order, UpdateOrderCommand command)
    {
        order.Id.Should().Be(command.OrderId);
        order.CustomerId.Should().Be(command.CustomerId);
        order.Items.Should().HaveCount(command.Items.Count);
        
        var expectedTotal = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        order.TotalAmount.Amount.Should().Be(expectedTotal);
    }

    public static void AssertValidOrderState(Order order)
    {
        order.Id.Should().NotBe(Guid.Empty);
        order.CustomerId.Should().NotBeNull();
        order.CustomerId.Should().NotBeEmpty();
        order.OrderDate.Should().BeAfter(DateTime.MinValue);
        order.CreatedAt.Should().BeAfter(DateTime.MinValue);
        order.UpdatedAt.Should().BeAfter(DateTime.MinValue);
        order.Items.Should().NotBeEmpty();
        order.TotalAmount.Amount.Should().BeGreaterThan(0);
    }
}