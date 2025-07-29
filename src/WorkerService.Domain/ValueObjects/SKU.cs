namespace WorkerService.Domain.ValueObjects;

public class SKU : IEquatable<SKU>
{
    public string Value { get; }

    public SKU(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty", nameof(value));
        
        if (value.Length > 50)
            throw new ArgumentException("SKU cannot exceed 50 characters", nameof(value));
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z0-9\-]+$"))
            throw new ArgumentException("SKU must contain only uppercase letters, numbers, and hyphens", nameof(value));

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as SKU);
    public bool Equals(SKU? other) => other != null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
    
    public static implicit operator string(SKU sku) => sku.Value;
    public static explicit operator SKU(string value) => new(value);
}