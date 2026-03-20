namespace GameCollection.Domain.ValueObjects;

public record GameName
{
    public string Value { get; }

    public GameName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("Game name cannot be empty.", nameof(value));
		}

		Value = value.Trim();
    }

    public override string ToString() => Value;
}
