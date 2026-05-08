namespace GameCollection.Domain.ValueObjects;

public record BggWeight
{
    public decimal Value { get; }

    public BggWeight(decimal value)
    {
        if (value is < 1.0m or > 5.0m)
        {
            throw new ArgumentException("BGG weight must be between 1.0 and 5.0.", nameof(value));
        }

        Value = Math.Round(value, 2);
    }

    public override string ToString() => Value.ToString("F2");
}
