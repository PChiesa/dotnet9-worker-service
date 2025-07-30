using System.Globalization;

namespace WorkerService.Domain.ValueObjects;

public class Price : IEquatable<Price>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Price(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Price cannot be negative", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public bool Equals(Price? other) => 
        other != null && Amount == other.Amount && Currency == other.Currency;
    
    public override bool Equals(object? obj) => Equals(obj as Price);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public override string ToString() => $"{Amount.ToString("F2", CultureInfo.InvariantCulture)} {Currency}";
}