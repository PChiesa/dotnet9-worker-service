using WorkerService.Domain.ValueObjects;

namespace WorkerService.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ProductId { get; private set; } = string.Empty; // Keep for backward compatibility
    public Guid? ItemId { get; private set; } // New reference to Item
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = new Money(0);
    public Money TotalPrice => new Money(UnitPrice.Amount * Quantity);

    // Navigation property
    public Item? Item { get; private set; }

    public OrderItem() { } // EF Core constructor

    // Existing constructor for backward compatibility
    public OrderItem(string productId, int quantity, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be empty", nameof(productId));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (unitPrice.Amount <= 0)
            throw new ArgumentException("Unit price must be greater than zero", nameof(unitPrice));

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    // New constructor with Item reference
    public OrderItem(Guid itemId, string productId, int quantity, Money unitPrice)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Item ID cannot be empty", nameof(itemId));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (unitPrice.Amount <= 0)
            throw new ArgumentException("Unit price must be greater than zero", nameof(unitPrice));

        Id = Guid.NewGuid();
        ItemId = itemId;
        ProductId = productId; // Keep for backward compatibility
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(newQuantity));

        Quantity = newQuantity;
    }

    public void LinkToItem(Guid itemId)
    {
        ItemId = itemId;
    }
}