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

    [Fact]
    public void UpdateCustomerId_WithValidCustomerId_ShouldUpdateCustomer()
    {
        // Arrange
        var order = CreateTestOrder();
        var originalUpdatedAt = order.UpdatedAt;

        // Act
        order.UpdateCustomerId("CUST999");

        // Assert
        order.CustomerId.Should().Be("CUST999");
        order.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateCustomerId_WithInvalidCustomerId_ShouldThrowException(string customerId)
    {
        // Arrange
        var order = CreateTestOrder();

        // Act & Assert
        var action = () => order.UpdateCustomerId(customerId);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Customer ID cannot be empty*");
    }

    [Fact]
    public void UpdateCustomerId_WithNullCustomerId_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act & Assert
        var action = () => order.UpdateCustomerId(null!);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Customer ID cannot be empty*");
    }

    [Fact]
    public void AddItem_ToPendingOrder_ShouldAddItemAndRecalculateTotal()
    {
        // Arrange
        var order = CreateTestOrder();
        var originalTotal = order.TotalAmount.Amount;
        var newItem = new OrderItem("PROD003", 1, new Money(15.00m));

        // Act
        order.AddItem(newItem);

        // Assert
        order.Items.Should().HaveCount(2);
        order.Items.Should().Contain(newItem);
        order.TotalAmount.Amount.Should().Be(originalTotal + 15.00m);
    }

    [Fact]
    public void AddItem_ToNonPendingOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder(); // Change status to Validated
        var newItem = new OrderItem("PROD003", 1, new Money(15.00m));

        // Act & Assert
        var action = () => order.AddItem(newItem);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Can only add items to pending orders");
    }

    [Fact]
    public void AddItem_WithNullItem_ShouldThrowArgumentNullException()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act & Assert
        var action = () => order.AddItem(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ClearItems_FromPendingOrder_ShouldRemoveAllItemsAndResetTotal()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.ClearItems();

        // Assert
        order.Items.Should().BeEmpty();
        order.TotalAmount.Amount.Should().Be(0);
    }

    [Fact]
    public void ClearItems_FromNonPendingOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder(); // Change status to Validated

        // Act & Assert
        var action = () => order.ClearItems();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Can only clear items from pending orders");
    }

    [Fact]
    public void MarkAsDeleted_FromNonDeliveredOrder_ShouldChangeStatusToCancelled()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();

        // Act
        order.MarkAsDeleted();

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().Contain(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void MarkAsDeleted_FromDeliveredOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsShipped();
        order.MarkAsDelivered();

        // Act & Assert
        var action = () => order.MarkAsDeleted();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot delete delivered orders");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder(); // This adds an event
        order.DomainEvents.Should().HaveCount(2); // OrderCreated + OrderValidated

        // Act
        order.ClearDomainEvents();

        // Assert
        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Order_Properties_ShouldBeSetCorrectlyOnCreation()
    {
        // Arrange
        var customerId = "CUST123";
        var items = new List<OrderItem>
        {
            new("PROD001", 3, new Money(25.50m))
        };

        // Act
        var order = new Order(customerId, items);

        // Assert
        order.Id.Should().NotBeEmpty();
        order.CustomerId.Should().Be(customerId);
        order.OrderDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        order.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Amount.Should().Be(76.50m); // 3 * 25.50
    }

    [Fact]
    public void MarkAsPaymentProcessing_FromValidatedOrder_ShouldChangeStatus()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        var originalUpdatedAt = order.UpdatedAt;

        // Act
        order.MarkAsPaymentProcessing();

        // Assert
        order.Status.Should().Be(OrderStatus.PaymentProcessing);
        order.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void MarkAsPaymentProcessing_FromNonValidatedOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder(); // Status is Pending

        // Act & Assert
        var action = () => order.MarkAsPaymentProcessing();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only validated orders can proceed to payment");
    }

    [Fact]
    public void MarkAsPaid_FromPaymentProcessingOrder_ShouldChangeStatusAndRaiseEvent()
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
    public void MarkAsPaid_FromNonPaymentProcessingOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder(); // Status is Validated, not PaymentProcessing

        // Act & Assert
        var action = () => order.MarkAsPaid();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only orders in payment processing can be marked as paid");
    }

    [Fact]
    public void MarkAsShipped_FromPaidOrder_ShouldChangeStatusAndRaiseEvent()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();

        // Act
        order.MarkAsShipped();

        // Assert
        order.Status.Should().Be(OrderStatus.Shipped);
        order.DomainEvents.Should().Contain(e => e is OrderShippedEvent);
    }

    [Fact]
    public void MarkAsShipped_FromNonPaidOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder(); // Status is Validated, not Paid

        // Act & Assert
        var action = () => order.MarkAsShipped();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only paid orders can be marked as shipped");
    }

    [Fact]
    public void MarkAsDelivered_FromShippedOrder_ShouldChangeStatus()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsShipped();

        // Act
        order.MarkAsDelivered();

        // Assert
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void MarkAsDelivered_FromNonShippedOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid(); // Status is Paid, not Shipped

        // Act & Assert
        var action = () => order.MarkAsDelivered();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Only shipped orders can be marked as delivered");
    }

    [Fact]
    public void ValidateOrder_WithEmptyItems_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ClearItems(); // Remove all items

        // Act & Assert
        var action = () => order.ValidateOrder();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot validate order without items");
    }

    [Fact]
    public void Cancel_AlreadyCancelledOrder_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.Cancel(); // Cancel once

        // Act & Assert
        var action = () => order.Cancel();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot cancel delivered or already cancelled orders");
    }

    [Fact]
    public void Order_WithMultipleItems_ShouldCalculateTotalCorrectly()
    {
        // Arrange
        var items = new List<OrderItem>
        {
            new("PROD001", 2, new Money(10.99m)),
            new("PROD002", 3, new Money(5.50m)),
            new("PROD003", 1, new Money(25.75m))
        };

        // Act
        var order = new Order("CUST001", items);

        // Assert
        // Total should be: (2 * 10.99) + (3 * 5.50) + (1 * 25.75) = 21.98 + 16.50 + 25.75 = 64.23
        order.TotalAmount.Amount.Should().Be(64.23m);
    }

    [Fact]
    public void Order_UpdatedAt_ShouldChangeWhenStatusChanges()
    {
        // Arrange
        var order = CreateTestOrder();
        var originalUpdatedAt = order.UpdatedAt;

        // Act
        Thread.Sleep(1); // Ensure time passes
        order.ValidateOrder();

        // Assert
        order.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Order_DomainEvents_ShouldAccumulateCorrectly()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.ValidateOrder();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsShipped();

        // Assert
        order.DomainEvents.Should().HaveCount(4); // Created, Validated, Paid, Shipped
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedEvent);
        order.DomainEvents.Should().ContainSingle(e => e is OrderValidatedEvent);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidEvent);
        order.DomainEvents.Should().ContainSingle(e => e is OrderShippedEvent);
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