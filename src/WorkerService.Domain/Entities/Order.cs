using WorkerService.Domain.ValueObjects;
using WorkerService.Domain.Events;

namespace WorkerService.Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; }
    public DateTime OrderDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    
    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Order() { } // EF Core constructor

    public Order(string customerId, IEnumerable<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));
        
        if (!items.Any())
            throw new ArgumentException("Order must contain at least one item", nameof(items));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        OrderDate = DateTime.UtcNow;
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        
        _items.AddRange(items);
        TotalAmount = CalculateTotalAmount();
        
        // Raise domain event
        _domainEvents.Add(new OrderCreatedEvent(Id, CustomerId, TotalAmount.Amount));
    }

    public void ValidateOrder()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be validated");
        
        if (!Items.Any())
            throw new InvalidOperationException("Cannot validate order without items");

        Status = OrderStatus.Validated;
        UpdatedAt = DateTime.UtcNow;
        
        _domainEvents.Add(new OrderValidatedEvent(Id, CustomerId));
    }

    public void MarkAsPaymentProcessing()
    {
        if (Status != OrderStatus.Validated)
            throw new InvalidOperationException("Only validated orders can proceed to payment");

        Status = OrderStatus.PaymentProcessing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.PaymentProcessing)
            throw new InvalidOperationException("Only orders in payment processing can be marked as paid");

        Status = OrderStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
        
        _domainEvents.Add(new OrderPaidEvent(Id, TotalAmount.Amount));
    }

    public void MarkAsShipped()
    {
        if (Status != OrderStatus.Paid)
            throw new InvalidOperationException("Only paid orders can be marked as shipped");

        Status = OrderStatus.Shipped;
        UpdatedAt = DateTime.UtcNow;
        
        _domainEvents.Add(new OrderShippedEvent(Id, CustomerId));
    }

    public void MarkAsDelivered()
    {
        if (Status != OrderStatus.Shipped)
            throw new InvalidOperationException("Only shipped orders can be marked as delivered");

        Status = OrderStatus.Delivered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot cancel delivered or already cancelled orders");

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        
        _domainEvents.Add(new OrderCancelledEvent(Id, CustomerId));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private Money CalculateTotalAmount()
    {
        var total = Items.Sum(item => item.UnitPrice.Amount * item.Quantity);
        return new Money(total);
    }
}