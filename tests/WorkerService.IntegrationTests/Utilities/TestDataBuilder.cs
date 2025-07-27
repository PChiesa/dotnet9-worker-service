using Bogus;
using WorkerService.Application.Commands;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;
using WorkerService.Domain.Events;

namespace WorkerService.IntegrationTests.Utilities;

public static class TestDataBuilder
{
    private static readonly Faker _faker = new();

    // Create Order Command builder
    public static Faker<CreateOrderCommand> GetCreateOrderCommandFaker()
    {
        return new Faker<CreateOrderCommand>()
            .RuleFor(o => o.CustomerId, f => f.Random.Guid().ToString())
            .RuleFor(o => o.Items, f => 
            {
                var items = new List<OrderItemDto>();
                var itemCount = f.Random.Int(1, 3);
                for (int i = 0; i < itemCount; i++)
                {
                    items.Add(new OrderItemDto(
                        f.Random.Guid().ToString(), // ProductId
                        f.Random.Int(1, 10), // Quantity
                        f.Random.Decimal(10, 1000) // UnitPrice
                    ));
                }
                return items;
            });
    }

    // Order entity builder for direct database insertion
    public static Faker<Order> GetOrderFaker()
    {
        return new Faker<Order>()
            .CustomInstantiator(f =>
            {
                var customerId = f.Random.Guid().ToString();
                var items = new List<OrderItem>();
                
                // Create order items first
                var itemCount = f.Random.Int(1, 3);
                for (int i = 0; i < itemCount; i++)
                {
                    var item = new OrderItem(
                        f.Random.Guid().ToString(), // ProductId
                        f.Random.Int(1, 10), // Quantity
                        new Money(f.Random.Decimal(10, 1000)) // UnitPrice
                    );
                    items.Add(item);
                }
                
                // Create order with items
                var order = new Order(customerId, items);
                return order;
            });
    }

    // Generate various test scenarios
    public static class Scenarios
    {
        // Valid order with all fields
        public static CreateOrderCommand ValidOrderWithAllFields()
        {
            return GetCreateOrderCommandFaker().Generate();
        }

        // Valid order with minimal fields
        public static CreateOrderCommand ValidOrderMinimalFields()
        {
            return new CreateOrderCommand(
                _faker.Random.Guid().ToString(), // CustomerId
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        _faker.Random.Guid().ToString(), // ProductId
                        _faker.Random.Int(1, 10), // Quantity
                        _faker.Random.Decimal(10, 100) // UnitPrice
                    )
                }
            );
        }

        // Invalid order - empty customer ID
        public static CreateOrderCommand InvalidOrderNoCustomer()
        {
            return new CreateOrderCommand(
                "", // Empty CustomerId
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        _faker.Random.Guid().ToString(),
                        _faker.Random.Int(1, 10),
                        _faker.Random.Decimal(10, 100)
                    )
                }
            );
        }

        // Invalid order - zero quantity
        public static CreateOrderCommand InvalidOrderZeroQuantity()
        {
            return new CreateOrderCommand(
                _faker.Random.Guid().ToString(),
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        _faker.Random.Guid().ToString(),
                        0, // Zero quantity
                        _faker.Random.Decimal(10, 100)
                    )
                }
            );
        }

        // Invalid order - negative price
        public static CreateOrderCommand InvalidOrderNegativePrice()
        {
            return new CreateOrderCommand(
                _faker.Random.Guid().ToString(),
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        _faker.Random.Guid().ToString(),
                        _faker.Random.Int(1, 10),
                        -10 // Negative price
                    )
                }
            );
        }

        // High value order
        public static CreateOrderCommand HighValueOrder()
        {
            return new CreateOrderCommand(
                _faker.Random.Guid().ToString(),
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        _faker.Random.Guid().ToString(),
                        _faker.Random.Int(100, 1000), // High quantity
                        _faker.Random.Decimal(1000, 10000) // High price
                    )
                }
            );
        }

        // Bulk order
        public static CreateOrderCommand BulkOrder()
        {
            return new CreateOrderCommand(
                _faker.Random.Guid().ToString(),
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        _faker.Random.Guid().ToString(),
                        _faker.Random.Int(1000, 10000), // Very high quantity
                        _faker.Random.Decimal(10, 100)
                    )
                }
            );
        }

        // Order with special customer ID (simulating edge case)
        public static CreateOrderCommand OrderWithSpecialCharacters()
        {
            return new CreateOrderCommand(
                "customer-with-special-chars-123", // Special customer ID
                new List<OrderItemDto>
                {
                    new OrderItemDto(
                        "product-with-special-chars-456", // Special product ID
                        _faker.Random.Int(1, 10),
                        _faker.Random.Decimal(10, 100)
                    )
                }
            );
        }

        // Generate multiple orders for the same customer
        public static List<CreateOrderCommand> MultipleOrdersSameCustomer(int count = 5)
        {
            var customerId = _faker.Random.Guid().ToString();
            var orders = new List<CreateOrderCommand>();

            for (int i = 0; i < count; i++)
            {
                orders.Add(new CreateOrderCommand(
                    customerId, // Same customer for all orders
                    new List<OrderItemDto>
                    {
                        new OrderItemDto(
                            _faker.Random.Guid().ToString(),
                            _faker.Random.Int(1, 10),
                            _faker.Random.Decimal(10, 1000)
                        )
                    }
                ));
            }

            return orders;
        }

        // Generate orders for testing (date range is handled by order creation time)
        public static List<CreateOrderCommand> OrdersForTesting(int count = 10)
        {
            var orders = new List<CreateOrderCommand>();

            for (int i = 0; i < count; i++)
            {
                orders.Add(new CreateOrderCommand(
                    _faker.Random.Guid().ToString(),
                    new List<OrderItemDto>
                    {
                        new OrderItemDto(
                            _faker.Random.Guid().ToString(),
                            _faker.Random.Int(1, 10),
                            _faker.Random.Decimal(10, 1000)
                        )
                    }
                ));
            }

            return orders;
        }
    }

    // Domain Events for testing
    public static class Events
    {
        public static OrderCreatedEvent ValidOrderCreatedEvent()
        {
            return new OrderCreatedEvent(
                Guid.NewGuid(), // OrderId
                _faker.Random.Guid().ToString(), // CustomerId
                _faker.Random.Decimal(10, 10000) // TotalAmount as decimal
            );
        }

        public static OrderCreatedEvent OrderCreatedEventFromCommand(CreateOrderCommand command, Guid orderId)
        {
            var totalAmount = command.Items.Sum(i => i.Quantity * i.UnitPrice);
            return new OrderCreatedEvent(
                orderId,
                command.CustomerId,
                totalAmount // TotalAmount as decimal
            );
        }
    }

    // Helper methods for test data generation
    public static class Helpers
    {
        public static string GenerateCustomerId()
        {
            return _faker.Random.Guid().ToString();
        }

        public static string GenerateProductId()
        {
            return _faker.Random.Guid().ToString();
        }

        public static OrderItemDto GenerateOrderItem()
        {
            return new OrderItemDto(
                GenerateProductId(),
                _faker.Random.Int(1, 100),
                _faker.Random.Decimal(0.01m, 9999.99m)
            );
        }

        public static List<OrderItemDto> GenerateOrderItems(int count = 3)
        {
            var items = new List<OrderItemDto>();
            for (int i = 0; i < count; i++)
            {
                items.Add(GenerateOrderItem());
            }
            return items;
        }

        public static Money GenerateMoney(decimal? amount = null)
        {
            return new Money(amount ?? _faker.Random.Decimal(1, 10000));
        }
    }
}