namespace NEXUS.SharedKernel.ValueObjects;

public class Money : BaseValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "GBP")
    {
        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");
        return new Money(Amount - other.Amount, Currency);
    }

    public override string ToString() => $"{Currency} {Amount:N2}";
}
