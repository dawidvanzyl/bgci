namespace GameCollection.Domain.ValueObjects;

public record BggGameId(int Value)
{
    public static BggGameId From(int value)
    {
        if (value <= 0)
            throw new ArgumentException("BGG Game ID must be a positive integer.", nameof(value));
        return new BggGameId(value);
    }

    public override string ToString() => Value.ToString();
}
