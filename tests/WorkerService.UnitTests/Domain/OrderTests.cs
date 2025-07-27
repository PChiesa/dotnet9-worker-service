using FluentAssertions;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Events;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Order_Creation_Should_Set_Properties_Correctly()
    {
        // Arrange
        var customerId = "CUST001";
        var items = new List<OrderItem>
        {
            new("PROD001", 2, new Money(10.00m)),
            new("PROD002", 1, new Money(25.50m))
        };

        // Act
        var order = new Order(customerId, items);

        // Assert
        order.Id.Should().NotBeEmpty();
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().HaveCount(2);
        order.TotalAmount.Amount.Should().Be(45.50m);
        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedEvent>();
    }

    [Fact]
    public void Order_Creation_With_Empty_CustomerId_Should_Throw_Exception()
    {
        // Arrange
        var customerId = "";
        var items = new List<OrderItem>
        {
            new("PROD001", 1, new Money(10.00m))
        };

        // Act & Assert
        var action = () => new Order(customerId, items);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Customer ID cannot be empty*");
    }

    [Fact]
    public void Order_Creation_With_No_Items_Should_Throw_Exception()
    {
        // Arrange
        var customerId = "CUST001";
        var items = new List<OrderItem>();

        // Act & Assert
        var action = () => new Order(customerId, items);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Order must contain at least one item*");
    }

    [Fact]
    public void ValidateOrder_Should_Change_Status_To_Validated()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.ValidateOrder();

        // Assert
        order.Status.Should().Be(OrderStatus.Validated);
        order.DomainEvents.Should().HaveCount(2); // OrderCreated + OrderValidated
        order.DomainEvents.Last().Should().BeOfType<OrderValidatedEvent>();
    }

    [Fact]
    public void ValidateOrder_On_Non_Pending_Order_Should_Throw_Exception()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder(); // Make it validated

        // Act & Assert
        var action = () => order.ValidateOrder();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only pending orders can be validated");
    }

    [Fact]
    public void MarkAsPaid_Should_Change_Status_And_Raise_Event()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();

        // Act
        order.MarkAsPaid();

        // Assert
        order.Status.Should().Be(OrderStatus.Paid);
        order.DomainEvents.Should().Contain(e => e is OrderPaidEvent);
    }

    [Fact]
    public void Cancel_Order_Should_Change_Status_And_Raise_Event()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Cancel();

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().Contain(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_Delivered_Order_Should_Throw_Exception()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsShipped();
        order.MarkAsDelivered();

        // Act & Assert
        var action = () => order.Cancel();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot cancel delivered or already cancelled orders");
    }

    [Fact]
    public void Order_State_Transitions_Should_Follow_Correct_Sequence()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act & Assert - Follow the complete workflow
        order.Status.Should().Be(OrderStatus.Pending);

        order.ValidateOrder();
        order.Status.Should().Be(OrderStatus.Validated);

        order.MarkAsPaymentProcessing();
        order.Status.Should().Be(OrderStatus.PaymentProcessing);

        order.MarkAsPaid();
        order.Status.Should().Be(OrderStatus.Paid);

        order.MarkAsShipped();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.MarkAsDelivered();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    private static Order CreateTestOrder()
    {
        var items = new List<OrderItem>
        {
            new("PROD001", 2, new Money(10.00m))
        };
        return new Order("CUST001", items);
    }
}