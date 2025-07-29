namespace WorkerService.Domain.ValueObjects;

public class StockLevel : IEquatable<StockLevel>
{
    public int Available { get; private set; }
    public int Reserved { get; private set; }
    public int Total => Available + Reserved;

    public StockLevel(int available, int reserved = 0)
    {
        if (available < 0)
            throw new ArgumentException("Available stock cannot be negative", nameof(available));
        
        if (reserved < 0)
            throw new ArgumentException("Reserved stock cannot be negative", nameof(reserved));

        Available = available;
        Reserved = reserved;
    }

    public StockLevel Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Reserve quantity must be positive", nameof(quantity));
        
        if (quantity > Available)
            throw new InvalidOperationException($"Cannot reserve {quantity} items. Only {Available} available.");

        return new StockLevel(Available - quantity, Reserved + quantity);
    }

    public StockLevel Release(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Release quantity must be positive", nameof(quantity));
        
        if (quantity > Reserved)
            throw new InvalidOperationException($"Cannot release {quantity} items. Only {Reserved} reserved.");

        return new StockLevel(Available + quantity, Reserved - quantity);
    }

    public StockLevel Adjust(int newAvailable)
    {
        if (newAvailable < 0)
            throw new ArgumentException("Stock level cannot be negative", nameof(newAvailable));

        return new StockLevel(newAvailable, Reserved);
    }

    public StockLevel Commit(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Commit quantity must be positive", nameof(quantity));
        
        if (quantity > Reserved)
            throw new InvalidOperationException($"Cannot commit {quantity} items. Only {Reserved} reserved.");

        return new StockLevel(Available, Reserved - quantity);
    }

    public bool Equals(StockLevel? other) => 
        other != null && Available == other.Available && Reserved == other.Reserved;
    
    public override bool Equals(object? obj) => Equals(obj as StockLevel);
    public override int GetHashCode() => HashCode.Combine(Available, Reserved);
    public override string ToString() => $"Available: {Available}, Reserved: {Reserved}";
}