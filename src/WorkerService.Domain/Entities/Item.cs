using WorkerService.Domain.ValueObjects;
using WorkerService.Domain.Events;

namespace WorkerService.Domain.Entities;

public class Item
{
    public Guid Id { get; private set; }
    public SKU SKU { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Price Price { get; private set; }
    public StockLevel StockLevel { get; private set; }
    public string Category { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } // For optimistic concurrency
    
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // EF Core constructor
    private Item() { }

    public Item(SKU sku, string name, string description, Price price, int initialStock, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name cannot be empty", nameof(name));
        
        if (name.Length > 200)
            throw new ArgumentException("Item name cannot exceed 200 characters", nameof(name));
        
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));

        Id = Guid.NewGuid();
        SKU = sku ?? throw new ArgumentNullException(nameof(sku));
        Name = name;
        Description = description ?? string.Empty;
        Price = price ?? throw new ArgumentNullException(nameof(price));
        StockLevel = new StockLevel(initialStock);
        Category = category;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new ItemCreatedEvent(Id, SKU.Value, Name, Price.Amount));
    }

    public void Update(string name, string description, Price price, string category)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update inactive item");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name cannot be empty", nameof(name));
        
        if (name.Length > 200)
            throw new ArgumentException("Item name cannot exceed 200 characters", nameof(name));

        var hasChanges = Name != name || 
                        Description != description || 
                        !Price.Equals(price) || 
                        Category != category;

        if (hasChanges)
        {
            Name = name;
            Description = description ?? string.Empty;
            Price = price ?? throw new ArgumentNullException(nameof(price));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            UpdatedAt = DateTime.UtcNow;
            Version = Guid.NewGuid().ToByteArray();

            _domainEvents.Add(new ItemUpdatedEvent(Id, SKU.Value, Name, Price.Amount));
        }
    }

    public void AdjustStock(int newQuantity)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot adjust stock for inactive item");

        var oldQuantity = StockLevel.Available;
        StockLevel = StockLevel.Adjust(newQuantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockAdjustedEvent(Id, SKU.Value, oldQuantity, newQuantity));
    }

    public void ReserveStock(int quantity)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot reserve stock for inactive item");

        StockLevel = StockLevel.Reserve(quantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockReservedEvent(Id, SKU.Value, quantity));
    }

    public void ReleaseStock(int quantity)
    {
        StockLevel = StockLevel.Release(quantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockReleasedEvent(Id, SKU.Value, quantity));
    }

    public void CommitStock(int quantity)
    {
        StockLevel = StockLevel.Commit(quantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockCommittedEvent(Id, SKU.Value, quantity));
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new ItemDeactivatedEvent(Id, SKU.Value));
    }

    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new ItemActivatedEvent(Id, SKU.Value));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}