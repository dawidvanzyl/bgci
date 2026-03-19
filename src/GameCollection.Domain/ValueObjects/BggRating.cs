namespace GameCollection.Domain.ValueObjects;

public record BggRating
{
    public decimal Value { get; }

    public BggRating(decimal value)
    {
        if (value is < 0 or > 10)
		{
			throw new ArgumentException("BGG rating must be between 0 and 10.", nameof(value));
		}

		Value = Math.Round(value, 1);
    }

    public override string ToString() => Value.ToString("F1");
}
