namespace WorkerService.Domain.ValueObjects;

public record Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative", nameof(amount));

        Amount = Math.Round(amount, 2);
    }

    public static Money Zero => new(0);

    public static Money operator +(Money left, Money right)
        => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right)
        => new(left.Amount - right.Amount);

    public static Money operator *(Money money, decimal multiplier)
        => new(money.Amount * multiplier);

    public static Money operator *(decimal multiplier, Money money)
        => new(multiplier * money.Amount);

    public static bool operator >(Money left, Money right)
        => left.Amount > right.Amount;

    public static bool operator <(Money left, Money right)
        => left.Amount < right.Amount;

    public static bool operator >=(Money left, Money right)
        => left.Amount >= right.Amount;

    public static bool operator <=(Money left, Money right)
        => left.Amount <= right.Amount;

    public override string ToString() => $"${Amount:F2}";
}